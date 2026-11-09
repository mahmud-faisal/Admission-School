using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Api;

/// <summary>
/// Spec §1.4: every field the spec lists must be present with the right JSON type and exact camelCase name.
/// Extra fields are allowed; missing or renamed ones are not.
/// </summary>
public class ContractShapeTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string UtcIso = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$";

    private static void Has(JsonElement json, string name, JsonValueKind kind)
    {
        json.TryGetProperty(name, out var value).Should().BeTrue($"field '{name}' is required by the spec");
        value.ValueKind.Should().Be(kind, $"field '{name}' must be a JSON {kind}");
    }

    private static void HasUtcTimestamp(JsonElement json, string name)
    {
        Has(json, name, JsonValueKind.String);
        json.GetProperty(name).GetString().Should().MatchRegex(UtcIso);
    }

    private static readonly string[] HoldStatuses = ["HELD", "CONFIRMED", "EXPIRED", "CANCELLED"];

    [Fact]
    public async Task AllEightEndpoints_ReturnSpecFieldsWithCorrectTypes()
    {
        // 1. POST /classes
        var created = await Client.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning Shift", totalSeats = 120 });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var cls = await ReadJsonAsync(created);
        Has(cls, "id", JsonValueKind.String);
        Has(cls, "name", JsonValueKind.String);
        Has(cls, "totalSeats", JsonValueKind.Number);
        var classId = cls.GetProperty("id").GetString()!;

        // 2. GET /classes and 3. GET /classes/{id}
        var list = await ReadJsonAsync(await Client.GetAsync("/classes"));
        list.ValueKind.Should().Be(JsonValueKind.Array);
        foreach (var item in new[] { list[0], await GetClassAsync(classId) })
        {
            Has(item, "id", JsonValueKind.String);
            Has(item, "name", JsonValueKind.String);
            foreach (var field in new[] { "totalSeats", "heldSeats", "confirmedSeats", "availableSeats" })
            {
                Has(item, field, JsonValueKind.Number);
            }
        }

        // 4. POST /classes/{id}/holds
        var hold = await ReadJsonAsync(await ApplyAsync(classId, Sid(1)));
        Has(hold, "holdId", JsonValueKind.String);
        Has(hold, "classId", JsonValueKind.String);
        Has(hold, "studentId", JsonValueKind.String);
        Has(hold, "status", JsonValueKind.String);
        HasUtcTimestamp(hold, "expiresAt");
        hold.GetProperty("status").GetString().Should().Be("HELD");
        var holdId = hold.GetProperty("holdId").GetString()!;

        // 5. GET /holds/{id}
        var view = await ReadJsonAsync(await Client.GetAsync($"/holds/{holdId}"));
        Has(view, "holdId", JsonValueKind.String);
        Has(view, "classId", JsonValueKind.String);
        Has(view, "status", JsonValueKind.String);
        HasUtcTimestamp(view, "expiresAt");
        view.GetProperty("status").GetString().Should().BeOneOf(HoldStatuses);

        // 6. POST /holds/{id}/confirm
        var confirmed = await ReadJsonAsync(await ConfirmAsync(holdId, Sid(1)));
        Has(confirmed, "holdId", JsonValueKind.String);
        Has(confirmed, "status", JsonValueKind.String);
        confirmed.GetProperty("status").GetString().Should().Be("CONFIRMED");

        // 8. GET /me/holds
        var mine = await ReadJsonAsync(await SendAsStudentAsync(HttpMethod.Get, "/me/holds", Sid(1)));
        mine.ValueKind.Should().Be(JsonValueKind.Array);
        Has(mine[0], "holdId", JsonValueKind.String);
        Has(mine[0], "classId", JsonValueKind.String);
        Has(mine[0], "className", JsonValueKind.String);
        Has(mine[0], "status", JsonValueKind.String);
        HasUtcTimestamp(mine[0], "expiresAt");

        // 7. DELETE /holds/{id} → 204 with an empty body
        var other = await ApplyOkAsync(classId, Sid(2));
        var deleted = await CancelAsync(other, Sid(2));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await deleted.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Errors_AreProblemJson_WithCodeTitleStatusAndTraceId()
    {
        var classId = await CreateClassAsync(seats: 1);
        await ApplyOkAsync(classId, Sid(1));

        var response = await ApplyAsync(classId, Sid(2));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await ReadJsonAsync(response);
        problem.GetProperty("code").GetString().Should().Be("SEATS_FULL");
        problem.GetProperty("status").GetInt32().Should().Be(409);
        Has(problem, "title", JsonValueKind.String);
        Has(problem, "detail", JsonValueKind.String);
        Has(problem, "traceId", JsonValueKind.String);
    }

    [Fact]
    public async Task UnexpectedException_Returns500_ProblemWithCode_AndNoStackTrace()
    {
        var failing = StartApiAsync(
            new Dictionary<string, string?>(),
            services => services.AddScoped<SchoolAdmission.Application.Abstractions.Persistence.IClassRepository, ThrowingClassRepository>());

        var response = await failing.GetAsync("/classes");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await ProblemCodeAsync(response)).Should().Be("INTERNAL_ERROR");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("ThrowingClassRepository").And.NotContain(" at ");
    }

    private sealed class ThrowingClassRepository : SchoolAdmission.Application.Abstractions.Persistence.IClassRepository
    {
        public void Add(SchoolAdmission.Domain.Classes.Class entity) => throw new InvalidOperationException("boom");

        public Task<SchoolAdmission.Application.Abstractions.Persistence.ClassLockRow?> LockForHoldCreationAsync(string classId, CancellationToken ct) =>
            throw new InvalidOperationException("boom");

        public Task<SchoolAdmission.Application.Abstractions.Persistence.ClassSeatsRow?> GetSeatsAsync(string classId, CancellationToken ct) =>
            throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<SchoolAdmission.Application.Abstractions.Persistence.ClassSeatsRow>> ListSeatsAsync(CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }
}
