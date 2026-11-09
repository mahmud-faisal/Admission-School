using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.UnitTests.Domain;

public class HoldRulesTests
{
    private const string Owner = "20152691234567890";
    private const string Other = "20152691234567891";
    private static readonly DateTime Now = new(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc);

    private static HoldSnapshot Snap(HoldStatus status, DateTime expiresAt, string student = Owner) =>
        new("h_1", student, status, expiresAt, Now);

    [Fact]
    public void EffectiveStatus_HeldBeforeExpiry_IsHeld() =>
        HoldRules.EffectiveStatus(HoldStatus.Held, Now.AddSeconds(1), Now).Should().Be(HoldStatus.Held);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EffectiveStatus_HeldAtOrAfterExpiry_IsExpired(int offsetSeconds) =>
        HoldRules.EffectiveStatus(HoldStatus.Held, Now.AddSeconds(offsetSeconds), Now).Should().Be(HoldStatus.Expired);

    [Theory]
    [InlineData(HoldStatus.Confirmed)]
    [InlineData(HoldStatus.Cancelled)]
    [InlineData(HoldStatus.Expired)]
    public void EffectiveStatus_TerminalStatuses_Unchanged(HoldStatus status) =>
        HoldRules.EffectiveStatus(status, Now.AddSeconds(-100), Now).Should().Be(status);

    [Fact]
    public void ClassifyConfirm_Missing_IsNotFound() =>
        HoldRules.ClassifyConfirm(null, Owner).Should().Be(ConfirmOutcome.NotFound);

    [Fact]
    public void ClassifyConfirm_OtherStudent_IsForbidden_EvenIfExpired() =>
        HoldRules.ClassifyConfirm(Snap(HoldStatus.Held, Now.AddSeconds(-5), Other), Owner)
            .Should().Be(ConfirmOutcome.Forbidden);

    [Fact]
    public void ClassifyConfirm_AlreadyConfirmed_IsIdempotentSuccess() =>
        HoldRules.ClassifyConfirm(Snap(HoldStatus.Confirmed, Now.AddSeconds(-5)), Owner)
            .Should().Be(ConfirmOutcome.AlreadyConfirmed);

    [Fact]
    public void ClassifyConfirm_Cancelled_IsHoldCancelled() =>
        HoldRules.ClassifyConfirm(Snap(HoldStatus.Cancelled, Now.AddSeconds(60)), Owner)
            .Should().Be(ConfirmOutcome.Cancelled);

    [Fact]
    public void ClassifyConfirm_StaleHeld_IsHoldExpired() =>
        HoldRules.ClassifyConfirm(Snap(HoldStatus.Held, Now), Owner).Should().Be(ConfirmOutcome.Expired);

    [Fact]
    public void ClassifyConfirm_ActiveHeld_IsStillHeld() =>
        HoldRules.ClassifyConfirm(Snap(HoldStatus.Held, Now.AddSeconds(60)), Owner).Should().Be(ConfirmOutcome.StillHeld);

    [Fact]
    public void ClassifyCancel_AlreadyCancelled_IsSuccess() =>
        HoldRules.ClassifyCancel(Snap(HoldStatus.Cancelled, Now.AddSeconds(60)), Owner)
            .Should().Be(CancelOutcome.AlreadyCancelled);

    [Fact]
    public void ClassifyCancel_Confirmed_IsAlreadyConfirmed() =>
        HoldRules.ClassifyCancel(Snap(HoldStatus.Confirmed, Now.AddSeconds(60)), Owner)
            .Should().Be(CancelOutcome.AlreadyConfirmed);

    [Theory]
    [InlineData(HoldStatus.Expired, 60)]
    [InlineData(HoldStatus.Held, -1)]
    public void ClassifyCancel_Expired_IsHoldExpired(HoldStatus stored, int expiresInSeconds) =>
        HoldRules.ClassifyCancel(Snap(stored, Now.AddSeconds(expiresInSeconds)), Owner)
            .Should().Be(CancelOutcome.Expired);

    [Fact]
    public void ClassifyCancel_OtherStudent_IsForbidden() =>
        HoldRules.ClassifyCancel(Snap(HoldStatus.Held, Now.AddSeconds(60), Other), Owner)
            .Should().Be(CancelOutcome.Forbidden);
}
