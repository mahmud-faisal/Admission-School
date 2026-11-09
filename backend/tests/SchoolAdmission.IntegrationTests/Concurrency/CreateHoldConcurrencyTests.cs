using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Concurrency;

/// <summary>
/// Spec §1.5 / §1.7: "send many requests at once and show that no more holds are granted than the number of seats".
/// All requests wait on a start gate and are released together.
/// </summary>
[Trait("Category", "Concurrency")]
public class CreateHoldConcurrencyTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private static async Task<HttpResponseMessage[]> FireTogether(int count, Func<int, Task<HttpResponseMessage>> request)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(1, count).Select(async i =>
        {
            await gate.Task;
            return await request(i);
        }).ToArray();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task CreateHold_200ParallelRequests_10Seats_ExactlyTenSucceed()
    {
        var classId = await CreateClassAsync(seats: 10);

        var responses = await FireTogether(200, i => ApplyAsync(classId, Sid(i)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(10);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(190);
        foreach (var conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            (await ProblemCodeAsync(conflict)).Should().Be("SEATS_FULL");
        }

        // Verify the database itself, not just the API responses.
        (await ScalarAsync<long>(
            "SELECT count(*) FROM holds WHERE class_id = @c AND status = 'HELD'", ("c", classId))).Should().Be(10);
        (await ScalarAsync<long>(
            "SELECT count(DISTINCT student_id) FROM holds WHERE class_id = @c", ("c", classId))).Should().Be(10);

        var seats = await GetClassAsync(classId);
        seats.GetProperty("heldSeats").GetInt32().Should().Be(10);
        seats.GetProperty("availableSeats").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task CreateHold_SameStudent50ParallelRequests_ExactlyOneSucceeds()
    {
        var classId = await CreateClassAsync(seats: 10);

        var responses = await FireTogether(50, _ => ApplyAsync(classId, Sid(1)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        foreach (var conflict in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await ProblemCodeAsync(conflict)).Should().Be("DUPLICATE_APPLICATION");
        }

        (await ScalarAsync<long>("SELECT count(*) FROM holds WHERE class_id = @c", ("c", classId))).Should().Be(1);
    }

    [Fact]
    public async Task CreateHold_ParallelAcrossClasses_EachClassCappedIndependently()
    {
        var a = await CreateClassAsync("A", seats: 5);
        var b = await CreateClassAsync("B", seats: 7);

        var responses = await FireTogether(120, i => ApplyAsync(i % 2 == 0 ? a : b, Sid(i)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(12);
        (await GetClassAsync(a)).GetProperty("heldSeats").GetInt32().Should().Be(5);
        (await GetClassAsync(b)).GetProperty("heldSeats").GetInt32().Should().Be(7);
    }
}
