using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Holds.CancelHold;
using SchoolAdmission.Application.Holds.ConfirmHold;
using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.UnitTests.Application.Handlers;

public class ConfirmAndCancelHandlerTests
{
    private const string Owner = "20152691234567890";
    private const string Other = "20152691234567891";
    private static readonly DateTime Now = new(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc);

    private readonly CallLog _log = new();
    private readonly FakeHoldRepository _holds;

    public ConfirmAndCancelHandlerTests() => _holds = new FakeHoldRepository(_log);

    private static HoldRow Row(HoldStatus status) =>
        new("h_1", "c_1", "Class", Owner, status, Now, Now.AddMinutes(10), status == HoldStatus.Confirmed ? Now : null, null, Now);

    private static HoldSnapshot Snap(HoldStatus status, int expiresIn = 600, string student = Owner) =>
        new("h_1", student, status, Now.AddSeconds(expiresIn), Now);

    private Task<Result<HoldConfirmedDto>> Confirm() =>
        new ConfirmHoldHandler(_holds).Handle(new ConfirmHoldCommand("h_1", Owner), CancellationToken.None);

    private Task<Result> Cancel() =>
        new CancelHoldHandler(_holds).Handle(new CancelHoldCommand("h_1", Owner), CancellationToken.None);

    [Fact]
    public async Task ConfirmHoldHandler_UpdateMatched_ReturnsConfirmed()
    {
        _holds.ConfirmResults.Enqueue(Row(HoldStatus.Confirmed));

        var result = await Confirm();

        result.Value!.Status.Should().Be(HoldStatus.Confirmed);
        _log.Calls.Should().Equal("TryConfirm");
    }

    [Fact]
    public async Task ConfirmHoldHandler_AlreadyConfirmed_IsIdempotent200()
    {
        _holds.Snapshots.Enqueue(Snap(HoldStatus.Confirmed));
        _holds.View = Row(HoldStatus.Confirmed);

        var result = await Confirm();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(HoldStatus.Confirmed);
    }

    public static TheoryData<HoldSnapshot?, string> ConfirmFailures => new()
    {
        { null, ErrorCodes.NotFound },
        { Snap(HoldStatus.Held, 600, Other), ErrorCodes.Forbidden },
        { Snap(HoldStatus.Cancelled), ErrorCodes.HoldCancelled },
        { Snap(HoldStatus.Expired), ErrorCodes.HoldExpired },
        { Snap(HoldStatus.Held, 0), ErrorCodes.HoldExpired },
    };

    [Theory]
    [MemberData(nameof(ConfirmFailures))]
    public async Task ConfirmHoldHandler_ZeroRows_UsesClassification(HoldSnapshot? snapshot, string expectedCode)
    {
        _holds.Snapshots.Enqueue(snapshot);

        var result = await Confirm();

        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task ConfirmHoldHandler_StillHeldAfterZeroRows_RetriesOnce()
    {
        _holds.ConfirmResults.Enqueue(null);
        _holds.ConfirmResults.Enqueue(Row(HoldStatus.Confirmed));
        _holds.Snapshots.Enqueue(Snap(HoldStatus.Held));

        var result = await Confirm();

        result.IsSuccess.Should().BeTrue();
        _log.Calls.Should().Equal("TryConfirm", "Snapshot", "TryConfirm");
    }

    [Fact]
    public async Task ConfirmHoldHandler_AlreadyConfirmedElsewhere_ReturnsAlreadyAdmitted()
    {
        _holds.ConfirmResults.Enqueue(null);
        _holds.Snapshots.Enqueue(Snap(HoldStatus.Cancelled));
        _holds.ConfirmedHold = new ConfirmedHoldRow("h_other", "c_other", "Class 5", Owner);

        var result = await Confirm();

        result.Error!.Code.Should().Be(ErrorCodes.AlreadyAdmitted);
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Message.Should().Contain("Class 5");
    }

    [Fact]
    public async Task CancelHoldHandler_Matched_Succeeds()
    {
        _holds.CancelResults.Enqueue(true);

        (await Cancel()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CancelHoldHandler_AlreadyCancelled_Succeeds()
    {
        _holds.Snapshots.Enqueue(Snap(HoldStatus.Cancelled));

        (await Cancel()).IsSuccess.Should().BeTrue();
    }

    public static TheoryData<HoldSnapshot?, string> CancelFailures => new()
    {
        { null, ErrorCodes.NotFound },
        { Snap(HoldStatus.Held, 600, Other), ErrorCodes.Forbidden },
        { Snap(HoldStatus.Confirmed), ErrorCodes.HoldAlreadyConfirmed },
        { Snap(HoldStatus.Held, -1), ErrorCodes.HoldExpired },
    };

    [Theory]
    [MemberData(nameof(CancelFailures))]
    public async Task CancelHoldHandler_ZeroRows_UsesClassification(HoldSnapshot? snapshot, string expectedCode)
    {
        _holds.Snapshots.Enqueue(snapshot);

        (await Cancel()).Error!.Code.Should().Be(expectedCode);
    }
}
