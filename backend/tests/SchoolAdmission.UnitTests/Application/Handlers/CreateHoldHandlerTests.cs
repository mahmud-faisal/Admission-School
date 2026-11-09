using Microsoft.Extensions.Options;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Common.Options;
using SchoolAdmission.Application.Holds.CreateHold;
using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.UnitTests.Application.Handlers;

public class CreateHoldHandlerTests
{
    private const string Student = "20152691234567890";
    private static readonly DateTime DbNow = new(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc);

    private readonly CallLog _log = new();
    private readonly FakeClassRepository _classes;
    private readonly FakeHoldRepository _holds;
    private readonly CreateHoldHandler _handler;

    public CreateHoldHandlerTests()
    {
        _classes = new FakeClassRepository(_log) { LockRow = new ClassLockRow("c_1", "Class 6 - Morning Shift", 2, DbNow) };
        _holds = new FakeHoldRepository(_log);
        _handler = new CreateHoldHandler(_classes, _holds, new FakeUnitOfWork(_log), new FakeIds(),
            Options.Create(new HoldOptions { HoldDurationSeconds = 120 }));
    }

    private Task<Result<HoldCreatedDto>> Run() =>
        _handler.Handle(new CreateHoldCommand("c_1", Student), CancellationToken.None);

    [Fact]
    public async Task CreateHoldHandler_ClassMissing_ReturnsNotFound_AndRollsBack()
    {
        _classes.LockRow = null;

        var result = await Run();

        result.Error!.Code.Should().Be(ErrorCodes.NotFound);
        _log.Calls.Should().Equal("Begin", "Lock", "Rollback");
    }

    [Fact]
    public async Task CreateHoldHandler_Duplicate_ReturnsDuplicateApplication_BeforeSeatCheck()
    {
        _holds.HasActive = true;
        _holds.Occupied = 2; // also full: the duplicate must win

        var result = await Run();

        result.Error!.Code.Should().Be(ErrorCodes.DuplicateApplication);
        _log.Calls.Should().NotContain("Count").And.NotContain("Insert").And.Contain("Rollback");
    }

    [Fact]
    public async Task CreateHoldHandler_Full_ReturnsSeatsFull()
    {
        _holds.Occupied = 2;

        var result = await Run();

        result.Error!.Code.Should().Be(ErrorCodes.SeatsFull);
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
        _log.Calls.Should().NotContain("Insert").And.Contain("Rollback");
    }

    [Fact]
    public async Task CreateHoldHandler_ExpiresStaleBeforeCounting()
    {
        var result = await Run();

        result.IsSuccess.Should().BeTrue();
        _log.Calls.Should().Equal("Begin", "Lock", "ExpireStale", "HasActive", "Count", "Insert", "Commit");
    }

    [Fact]
    public async Task CreateHoldHandler_UniqueViolation_MapsToDuplicateApplication()
    {
        _holds.ThrowDuplicateOnInsert = true;

        var result = await Run();

        result.Error!.Code.Should().Be(ErrorCodes.DuplicateApplication);
        _log.Calls.Should().EndWith("Rollback");
    }

    [Fact]
    public async Task CreateHoldHandler_Success_ReturnsHeld_WithConfiguredDuration()
    {
        _holds.Occupied = 1;

        var result = await Run();

        result.Value!.Status.Should().Be(HoldStatus.Held);
        result.Value.StudentId.Should().Be(Student);
        result.Value.ClassName.Should().Be("Class 6 - Morning Shift");
        result.Value.ServerTime.Should().Be(DbNow);
        _holds.InsertedDuration.Should().Be(120);
    }
}
