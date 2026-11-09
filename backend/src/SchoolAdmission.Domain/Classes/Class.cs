namespace SchoolAdmission.Domain.Classes;

/// <summary>One admission class. Each shift is its own class with its own seat count (spec §1.3).</summary>
public sealed class Class
{
    public const int MaxNameLength = 200;
    public const int MinSeats = 1;
    public const int MaxSeats = 100_000;

    // For EF Core materialization.
    private Class()
    {
        Id = string.Empty;
        Name = string.Empty;
    }

    private Class(string id, string name, int totalSeats)
    {
        Id = id;
        Name = name;
        TotalSeats = totalSeats;
    }

    /// <summary>"c_" + 32 lowercase hex characters.</summary>
    public string Id { get; private set; }

    public string Name { get; private set; }

    public int TotalSeats { get; private set; }

    /// <summary>UTC. Set by the database (<c>DEFAULT now()</c>) on insert, so every process uses the DB clock.</summary>
    public DateTime CreatedAt { get; private set; }

    public static Class Create(string id, string name, int totalSeats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            throw new ArgumentException($"Name must be 1..{MaxNameLength} characters after trimming.", nameof(name));
        }

        if (totalSeats is < MinSeats or > MaxSeats)
        {
            throw new ArgumentOutOfRangeException(nameof(totalSeats), totalSeats, $"Seats must be {MinSeats}..{MaxSeats}.");
        }

        return new Class(id, trimmed, totalSeats);
    }
}
