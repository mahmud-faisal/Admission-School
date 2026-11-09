using System.Net;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Holds.ExpireStaleHolds;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Expiry;

/// <summary>
/// Spec §1.5: when a hold expires the seat is released automatically, with nobody making an API call for it.
/// These tests turn the sweeper OFF to prove expiry is computed on read with the database clock.
/// </summary>
public class ExpiryTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const int HoldSeconds = 2;

    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["HOLD_DURATION_SECONDS"] = HoldSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["HOLD_SWEEPER_ENABLED"] = "false",
    };

    // expires_at is rounded up to a whole second, so a hold lasts between HoldSeconds and HoldSeconds + 1.
    private static Task WaitForExpiry() => Task.Delay(TimeSpan.FromSeconds(HoldSeconds + 1.5));

    [Fact]
    public async Task Expiry_HoldLastsAtLeastHoldDurationSeconds()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));

        await Task.Delay(TimeSpan.FromSeconds(HoldSeconds - 0.5));

        (await ReadJsonAsync(await Client.GetAsync($"/holds/{holdId}"))).GetProperty("status").GetString().Should().Be("HELD");
        (await ConfirmAsync(holdId, Sid(1))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Expiry_SeatReleasedWithoutApiCallOrSweeper()
    {
        var classId = await CreateClassAsync(seats: 2);
        var holdId = await ApplyOkAsync(classId, Sid(1));
        (await GetClassAsync(classId)).GetProperty("heldSeats").GetInt32().Should().Be(1);

        await WaitForExpiry();

        var seats = await GetClassAsync(classId);
        seats.GetProperty("heldSeats").GetInt32().Should().Be(0);
        seats.GetProperty("availableSeats").GetInt32().Should().Be(2);
        (await ReadJsonAsync(await Client.GetAsync($"/holds/{holdId}"))).GetProperty("status").GetString().Should().Be("EXPIRED");
        var mine = await ReadJsonAsync(await SendAsStudentAsync(HttpMethod.Get, "/me/holds", Sid(1)));
        mine[0].GetProperty("status").GetString().Should().Be("EXPIRED");

        // Proof it was computed on read: nothing has written to the row yet.
        (await ScalarAsync<string>("SELECT status FROM holds WHERE id = @h", ("h", holdId))).Should().Be("HELD");
    }

    [Fact]
    public async Task Expiry_ConfirmAfterExpiry_Returns409_HoldExpired()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));

        await WaitForExpiry();
        var response = await ConfirmAsync(holdId, Sid(1));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(response)).Should().Be("HOLD_EXPIRED");
        (await GetClassAsync(classId)).GetProperty("confirmedSeats").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Expiry_CancelAfterExpiry_Returns409_HoldExpired()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));

        await WaitForExpiry();
        var response = await CancelAsync(holdId, Sid(1));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(response)).Should().Be("HOLD_EXPIRED");
    }

    [Fact]
    public async Task Expiry_ReapplyAfterExpiry_NotBlockedByUnsweptRow_Returns201()
    {
        var classId = await CreateClassAsync();
        var oldHold = await ApplyOkAsync(classId, Sid(1));

        await WaitForExpiry();
        // The stale row is still HELD in the table and still inside the partial unique index.
        (await ScalarAsync<string>("SELECT status FROM holds WHERE id = @h", ("h", oldHold))).Should().Be("HELD");

        var newHold = await ApplyOkAsync(classId, Sid(1));

        newHold.Should().NotBe(oldHold);
        (await ScalarAsync<string>("SELECT status FROM holds WHERE id = @h", ("h", oldHold))).Should().Be("EXPIRED");
    }

    [Fact]
    public async Task Expiry_FullClassFreesSeatOnExpiry_NextApplicantGets201()
    {
        var classId = await CreateClassAsync(seats: 1);
        await ApplyOkAsync(classId, Sid(1));
        (await ApplyAsync(classId, Sid(2))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await WaitForExpiry();

        await ApplyOkAsync(classId, Sid(2));
    }

    [Fact]
    public async Task Expiry_ConfirmedHoldNeverExpires()
    {
        var classId = await CreateClassAsync(seats: 1);
        var holdId = await ApplyOkAsync(classId, Sid(1));
        (await ConfirmAsync(holdId, Sid(1))).StatusCode.Should().Be(HttpStatusCode.OK);

        await WaitForExpiry();

        (await ReadJsonAsync(await Client.GetAsync($"/holds/{holdId}"))).GetProperty("status").GetString().Should().Be("CONFIRMED");
        (await GetClassAsync(classId)).GetProperty("confirmedSeats").GetInt32().Should().Be(1);
        (await ApplyAsync(classId, Sid(2))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}

/// <summary>The background sweeper writes EXPIRED onto stale rows, and is safe to run in several processes at once.</summary>
public class SweeperTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["HOLD_DURATION_SECONDS"] = "1",
        ["HOLD_SWEEPER_ENABLED"] = "true",
        ["HOLD_SWEEP_INTERVAL_SECONDS"] = "1",
    };

    [Fact]
    public async Task Sweeper_MarksStaleHoldsExpired()
    {
        var classId = await CreateClassAsync(seats: 5);
        var holdId = await ApplyOkAsync(classId, Sid(1));

        var deadline = DateTime.UtcNow.AddSeconds(15);
        string? status = null;
        while (DateTime.UtcNow < deadline)
        {
            status = await ScalarAsync<string>("SELECT status FROM holds WHERE id = @h", ("h", holdId));
            if (status == "EXPIRED")
            {
                break;
            }

            await Task.Delay(250);
        }

        status.Should().Be("EXPIRED");
        (await ScalarAsync<long>(
            "SELECT count(*) FROM holds WHERE id = @h AND expired_at IS NOT NULL", ("h", holdId))).Should().Be(1);
    }
}

/// <summary>Two sweepers (two service providers, like two processes) on one database; this class keeps the hosted sweeper OFF.</summary>
public class SweeperConcurrencyTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    [Fact]
    public async Task Sweeper_TwoConcurrentSweepers_NoErrors_AllExpiredOnce()
    {
        // Seed 300 already-stale HELD rows directly.
        var classId = await CreateClassAsync(seats: 1000);
        await ExecuteSqlAsync(
            """
            INSERT INTO holds (id, class_id, student_id, status, created_at, expires_at)
            SELECT 'h_seed_' || g, @c, (20160000000000000 + g)::text, 'HELD', now() - interval '1 hour', now() - interval '1 minute'
            FROM generate_series(1, 300) AS g
            """,
            ("c", classId));

        // Two API instances (separate service providers) sweep the same database in parallel, small batches.
        var settings = new Dictionary<string, string?> { ["HOLD_SWEEPER_ENABLED"] = "false" };
        var factories = new[] { new AdmissionApiFactory(ConnectionString, settings), new AdmissionApiFactory(ConnectionString, settings) };
        try
        {
            var sweeps = factories.SelectMany(f => Enumerable.Range(0, 3).Select(async _ =>
            {
                await using var scope = f.Services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpireStaleHoldsCommand(25));
            })).ToArray();

            var counts = await Task.WhenAll(sweeps);

            counts.Sum().Should().Be(300, "each stale row is expired exactly once");
            (await ScalarAsync<long>("SELECT count(*) FROM holds WHERE status = 'HELD'")).Should().Be(0);
            (await ScalarAsync<long>("SELECT count(*) FROM holds WHERE status = 'EXPIRED'")).Should().Be(300);
        }
        finally
        {
            foreach (var factory in factories)
            {
                await factory.DisposeAsync();
            }
        }
    }
}
