using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Concurrency;

[Trait("Category", "Concurrency")]
public class ConfirmConcurrencyTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    // Default 600 s holds: this test must not race expiry.
    [Fact]
    public async Task ConfirmHold_50ParallelConfirms_AllReturn200_ConfirmedSeatsIsOne()
    {
        var classId = await CreateClassAsync(seats: 3);
        var holdId = await ApplyOkAsync(classId, Sid(1));

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            await gate.Task;
            return await ConfirmAsync(holdId, Sid(1));
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var seats = await GetClassAsync(classId);
        seats.GetProperty("confirmedSeats").GetInt32().Should().Be(1);
        seats.GetProperty("heldSeats").GetInt32().Should().Be(0);
        (await ScalarAsync<long>("SELECT count(*) FROM holds WHERE status = 'CONFIRMED'")).Should().Be(1);
    }

    [Fact]
    public async Task ConfirmHold_ParallelConfirmsDifferentHolds_SameStudent_ExactlyOneSucceeds()
    {
        var classA = await CreateClassAsync("Class A", seats: 5);
        var classB = await CreateClassAsync("Class B", seats: 5);
        var holdA = await ApplyOkAsync(classA, Sid(1));
        var holdB = await ApplyOkAsync(classB, Sid(1));

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new[]
        {
            Task.Run(async () => { await gate.Task; return await ConfirmAsync(holdA, Sid(1)); }),
            Task.Run(async () => { await gate.Task; return await ConfirmAsync(holdB, Sid(1)); }),
        };
        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        (await ScalarAsync<long>("SELECT count(*) FROM holds WHERE student_id = @s AND status = 'CONFIRMED'", ("s", Sid(1))))
            .Should().Be(1);
        (await ScalarAsync<long>("SELECT count(*) FROM holds WHERE student_id = @s AND status = 'CANCELLED'", ("s", Sid(1))))
            .Should().Be(1);
    }
}

/// <summary>Short (2 s) holds so expiry happens during the run; kept apart so other tests are not timing-sensitive.</summary>
[Trait("Category", "Concurrency")]
public class MixedLoadConcurrencyTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    protected override IReadOnlyDictionary<string, string?> Settings =>
        new Dictionary<string, string?> { ["HOLD_DURATION_SECONDS"] = "2" };

    /// <summary>
    /// Random applies, confirms and cancels from many students for a few seconds, with 2-second holds so expiry also
    /// happens. A monitor reads the database every 50 ms: held (not expired) + confirmed must never exceed total.
    /// </summary>
    [Fact]
    public async Task MixedLoad_HoldsConfirmsCancels_NeverExceedTotalSeats()
    {
        const int totalSeats = 5;
        var classId = await CreateClassAsync(seats: totalSeats);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var violations = new List<long>();
        var samples = 0;

        var monitor = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                var occupied = await ScalarAsync<long>(
                    """
                    SELECT count(*) FROM holds
                    WHERE class_id = @c AND ((status = 'HELD' AND expires_at > now()) OR status = 'CONFIRMED')
                    """,
                    ("c", classId));
                samples++;
                if (occupied > totalSeats)
                {
                    lock (violations)
                    {
                        violations.Add(occupied);
                    }
                }

                await Task.Delay(50);
            }
        });

        var workers = Enumerable.Range(0, 12).Select(worker => Task.Run(async () =>
        {
            var random = new Random(worker);
            while (!stop.IsCancellationRequested)
            {
                var student = Sid((worker * 1000) + random.Next(5));
                var apply = await ApplyAsync(classId, student);
                if (apply.StatusCode != HttpStatusCode.Created)
                {
                    continue;
                }

                var holdId = (await ReadJsonAsync(apply)).GetProperty("holdId").GetString()!;
                switch (random.Next(3))
                {
                    case 0:
                        await ConfirmAsync(holdId, student);
                        break;
                    case 1:
                        await CancelAsync(holdId, student);
                        break;
                    default:
                        break; // let it expire
                }
            }
        })).ToArray();

        await Task.WhenAll(workers);
        await monitor;

        samples.Should().BeGreaterThan(10);
        violations.Should().BeEmpty();
        var final = await GetClassAsync(classId);
        (final.GetProperty("heldSeats").GetInt32() + final.GetProperty("confirmedSeats").GetInt32())
            .Should().BeLessThanOrEqualTo(totalSeats);
    }
}
