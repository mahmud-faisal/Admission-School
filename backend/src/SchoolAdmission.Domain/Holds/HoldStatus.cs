namespace SchoolAdmission.Domain.Holds;

/// <summary>Stored and serialized as HELD / CONFIRMED / CANCELLED / EXPIRED.</summary>
public enum HoldStatus
{
    Held,
    Confirmed,
    Cancelled,
    Expired,
}

/// <summary>The UPPERCASE text form used in the database and in JSON.</summary>
public static class HoldStatusNames
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";

    public static string ToDb(this HoldStatus status) => status switch
    {
        HoldStatus.Held => Held,
        HoldStatus.Confirmed => Confirmed,
        HoldStatus.Cancelled => Cancelled,
        HoldStatus.Expired => Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static HoldStatus FromDb(string value) => value switch
    {
        Held => HoldStatus.Held,
        Confirmed => HoldStatus.Confirmed,
        Cancelled => HoldStatus.Cancelled,
        Expired => HoldStatus.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown hold status."),
    };
}
