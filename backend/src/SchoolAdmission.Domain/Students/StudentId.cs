namespace SchoolAdmission.Domain.Students;

/// <summary>A student's birth registration number: exactly 17 ASCII digits.</summary>
public readonly record struct StudentId
{
    public const int Length = 17;

    private StudentId(string value) => Value = value;

    public string Value { get; }

    /// <summary>Only ASCII 0-9 count as digits; char.IsDigit would also accept non-ASCII Unicode digits.</summary>
    public static bool IsValid(string? raw)
    {
        if (raw is null || raw.Length != Length)
        {
            return false;
        }

        foreach (var c in raw)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryCreate(string? raw, out StudentId id)
    {
        if (IsValid(raw))
        {
            id = new StudentId(raw!);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value;
}
