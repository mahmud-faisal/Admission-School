using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Concurrency;

/// <summary>
/// Bonus (spec §1.6): two backend PROCESSES on different ports, same database, must never oversell.
/// This starts two real OS processes (<c>dotnet SchoolAdmission.Api.dll</c>), so no in-process lock could help.
/// </summary>
[Trait("Category", "MultiProcess")]
public class TwoProcessOversellTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    [Fact]
    public async Task TwoApiProcesses_SameDb_300Requests_10Seats_ExactlyTenSucceed()
    {
        var apiDll = ApiProcess.FindApiDll();
        Assert.SkipWhen(apiDll is null, "SchoolAdmission.Api.dll not found next to the tests and API_DLL_PATH not set.");

        var env = new Dictionary<string, string> { ["HOLD_SWEEPER_ENABLED"] = "false" };
        await using var first = await ApiProcess.StartAsync(apiDll!, ConnectionString, env);
        await using var second = await ApiProcess.StartAsync(apiDll!, ConnectionString, env);
        using var clientA = first.CreateClient();
        using var clientB = second.CreateClient();
        first.BaseAddress.Port.Should().NotBe(second.BaseAddress.Port);

        var classId = await CreateClassAsync(seats: 10, client: clientA);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(1, 300).Select(async i =>
        {
            await gate.Task;
            return await ApplyAsync(i % 2 == 0 ? clientA : clientB, classId, Sid(i));
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var full = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        created.Should().Be(10, $"201s: {created}, 409s: {full}, other: {300 - created - full}");
        full.Should().Be(290);

        (await ScalarAsync<long>(
            "SELECT count(*) FROM holds WHERE class_id = @c AND status = 'HELD'", ("c", classId))).Should().Be(10);

        // Both processes report the same seat counts.
        foreach (var client in new[] { clientA, clientB })
        {
            var seats = await ReadJsonAsync(await client.GetAsync($"/classes/{classId}"));
            seats.GetProperty("heldSeats").GetInt32().Should().Be(10);
            seats.GetProperty("availableSeats").GetInt32().Should().Be(0);
        }
    }

    [Fact]
    public async Task TwoApiProcesses_ParallelConfirmsOfSameHold_ConfirmedSeatsIsOne()
    {
        var apiDll = ApiProcess.FindApiDll();
        Assert.SkipWhen(apiDll is null, "SchoolAdmission.Api.dll not found next to the tests and API_DLL_PATH not set.");

        var env = new Dictionary<string, string> { ["HOLD_SWEEPER_ENABLED"] = "false" };
        await using var first = await ApiProcess.StartAsync(apiDll!, ConnectionString, env);
        await using var second = await ApiProcess.StartAsync(apiDll!, ConnectionString, env);
        using var clientA = first.CreateClient();
        using var clientB = second.CreateClient();

        var classId = await CreateClassAsync(seats: 2, client: clientA);
        var hold = await ReadJsonAsync(await ApplyAsync(clientA, classId, Sid(1)));
        var holdId = hold.GetProperty("holdId").GetString()!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(i => ConfirmAsync(holdId, Sid(1), i % 2 == 0 ? clientA : clientB)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await ScalarAsync<long>("SELECT count(*) FROM holds WHERE status = 'CONFIRMED'")).Should().Be(1);
    }
}
