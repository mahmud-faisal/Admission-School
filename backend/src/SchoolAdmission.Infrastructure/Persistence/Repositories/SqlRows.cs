using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.Infrastructure.Persistence.Repositories;

// Unmapped result types for Database.SqlQuery<T>. SQL aliases match these property names exactly.
// They are mutable classes (not records) so EF's materializer can set them without constructor binding.

internal sealed class ClassLockSqlRow
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int TotalSeats { get; set; }
}

internal sealed class ClassSeatsSqlRow
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int TotalSeats { get; set; }

    public int HeldSeats { get; set; }

    public int ConfirmedSeats { get; set; }

    public int AvailableSeats { get; set; }

    public DateTime CreatedAt { get; set; }

    public ClassSeatsRow ToRow() =>
        new(Id, Name, TotalSeats, HeldSeats, ConfirmedSeats, AvailableSeats, CreatedAt);
}

internal sealed class HoldSqlRow
{
    public string Id { get; set; } = string.Empty;

    public string ClassId { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string StudentId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime DbNow { get; set; }

    public HoldRow ToRow() =>
        new(Id, ClassId, ClassName, StudentId, HoldStatusNames.FromDb(Status), CreatedAt, ExpiresAt,
            ConfirmedAt, CancelledAt, DbNow);
}

internal sealed class HoldSnapshotSqlRow
{
    public string Id { get; set; } = string.Empty;

    public string StudentId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime DbNow { get; set; }

    public HoldSnapshot ToSnapshot() => new(Id, StudentId, HoldStatusNames.FromDb(Status), ExpiresAt, DbNow);
}

internal sealed class ConfirmedHoldSqlRow
{
    public string HoldId { get; set; } = string.Empty;

    public string ClassId { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string StudentId { get; set; } = string.Empty;

    public ConfirmedHoldRow ToRow() => new(HoldId, ClassId, ClassName, StudentId);
}
