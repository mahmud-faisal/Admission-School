using Npgsql;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Persistence;

/// <summary>The database itself enforces the invariants, even if application code were bypassed.</summary>
public class SchemaTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string InsertHold = """
        INSERT INTO holds (id, class_id, student_id, status, created_at, expires_at, confirmed_at)
        VALUES (@id, @class, @student, @status, now(), now() + interval '10 minutes',
                CASE WHEN @status = 'CONFIRMED' THEN now() END)
        """;

    private Task Insert(string id, string classId, string student, string status) =>
        ExecuteSqlAsync(InsertHold, ("id", id), ("class", classId), ("student", student), ("status", status));

    [Fact]
    public async Task SchemaTests_PartialUniqueIndex_RejectsSecondActiveRow()
    {
        var classId = await CreateClassAsync();
        await Insert("h_1", classId, Sid(1), "HELD");

        var duplicateHeld = () => Insert("h_2", classId, Sid(1), "HELD");
        var duplicateConfirmed = () => Insert("h_3", classId, Sid(1), "CONFIRMED");

        (await duplicateHeld.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ux_holds_class_student_active");
        (await duplicateConfirmed.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SchemaTests_PartialUniqueIndex_AllowsInactiveHistory()
    {
        var classId = await CreateClassAsync();
        await Insert("h_1", classId, Sid(1), "CANCELLED");
        await Insert("h_2", classId, Sid(1), "EXPIRED");
        await Insert("h_3", classId, Sid(1), "HELD");

        (await ScalarAsync<long>("SELECT count(*) FROM holds")).Should().Be(3);
    }

    [Theory]
    [InlineData("h_1", "PENDING", "20152691234567890")]
    [InlineData("h_1", "HELD", "2015269123456789")]
    [InlineData("h_1", "HELD", "2015269123456789x")]
    public async Task SchemaTests_CheckConstraints_RejectBadStatusAndStudentId(string id, string status, string student)
    {
        var classId = await CreateClassAsync();

        var act = () => Insert(id, classId, student, status);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100001)]
    public async Task SchemaTests_ClassSeatsCheck_RejectsOutOfRange(int seats)
    {
        var act = () => ExecuteSqlAsync(
            "INSERT INTO classes (id, name, total_seats) VALUES ('c_x', 'X', @seats)", ("seats", seats));

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }
}
