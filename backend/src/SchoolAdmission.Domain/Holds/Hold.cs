namespace SchoolAdmission.Domain.Holds;

/// <summary>
/// A seat hold (an application). State changes are made by conditional SQL UPDATEs in the database,
/// never by mutating this object, so this type only maps the table and has no public setters.
/// </summary>
public sealed class Hold
{
    // For EF Core materialization.
    private Hold()
    {
        Id = string.Empty;
        ClassId = string.Empty;
        StudentId = string.Empty;
    }

    /// <summary>"h_" + 32 lowercase hex characters.</summary>
    public string Id { get; private set; }

    public string ClassId { get; private set; }

    /// <summary>17 ASCII digits.</summary>
    public string StudentId { get; private set; }

    /// <summary>Stored status. May be a stale HELD; use <see cref="HoldRules.EffectiveStatus"/>.</summary>
    public HoldStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? ConfirmedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public DateTime? ExpiredAt { get; private set; }
}
