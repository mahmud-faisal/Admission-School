namespace SchoolAdmission.Domain.Holds;

public enum ConfirmOutcome
{
    NotFound,
    Forbidden,
    AlreadyConfirmed,
    Cancelled,
    Expired,

    /// <summary>Still HELD and not expired, so the UPDATE should have matched (it lost a race). Retry.</summary>
    StillHeld,
}

public enum CancelOutcome
{
    NotFound,
    Forbidden,
    AlreadyCancelled,
    AlreadyConfirmed,
    Expired,

    /// <summary>Still HELD and not expired, so the UPDATE should have matched (it lost a race). Retry.</summary>
    StillHeld,
}

/// <summary>
/// Pure decision logic. The database conditional UPDATE is what actually changes state; these rules only
/// compute the effective status and explain why an UPDATE matched no row. All times come from the DB clock.
/// </summary>
public static class HoldRules
{
    /// <summary>A HELD hold whose expiry has passed is EXPIRED, whatever the stored value says.</summary>
    public static HoldStatus EffectiveStatus(HoldStatus stored, DateTime expiresAtUtc, DateTime dbNowUtc) =>
        stored == HoldStatus.Held && expiresAtUtc <= dbNowUtc ? HoldStatus.Expired : stored;

    /// <summary>Ownership is checked before status, so other students learn nothing about the hold's state.</summary>
    public static ConfirmOutcome ClassifyConfirm(HoldSnapshot? hold, string studentId)
    {
        if (hold is null)
        {
            return ConfirmOutcome.NotFound;
        }

        if (!string.Equals(hold.StudentId, studentId, StringComparison.Ordinal))
        {
            return ConfirmOutcome.Forbidden;
        }

        return EffectiveStatus(hold.Status, hold.ExpiresAt, hold.DbNow) switch
        {
            HoldStatus.Confirmed => ConfirmOutcome.AlreadyConfirmed,
            HoldStatus.Cancelled => ConfirmOutcome.Cancelled,
            HoldStatus.Expired => ConfirmOutcome.Expired,
            _ => ConfirmOutcome.StillHeld,
        };
    }

    public static CancelOutcome ClassifyCancel(HoldSnapshot? hold, string studentId)
    {
        if (hold is null)
        {
            return CancelOutcome.NotFound;
        }

        if (!string.Equals(hold.StudentId, studentId, StringComparison.Ordinal))
        {
            return CancelOutcome.Forbidden;
        }

        return EffectiveStatus(hold.Status, hold.ExpiresAt, hold.DbNow) switch
        {
            HoldStatus.Cancelled => CancelOutcome.AlreadyCancelled,
            HoldStatus.Confirmed => CancelOutcome.AlreadyConfirmed,
            HoldStatus.Expired => CancelOutcome.Expired,
            _ => CancelOutcome.StillHeld,
        };
    }
}
