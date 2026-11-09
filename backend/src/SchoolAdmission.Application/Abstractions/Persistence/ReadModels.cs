using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.Application.Abstractions.Persistence;

/// <summary>The class row locked with FOR UPDATE, plus the DB clock read after the lock was acquired.</summary>
public sealed record ClassLockRow(string Id, string Name, int TotalSeats, DateTime DbNow);

/// <summary>A class with seat counts computed in SQL using the DB clock.</summary>
public sealed record ClassSeatsRow(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats,
    DateTime CreatedAt);

/// <summary>A hold joined with its class. <see cref="Status"/> is the EFFECTIVE status (expiry applied with the DB clock).</summary>
public sealed record HoldRow(
    string Id,
    string ClassId,
    string ClassName,
    string StudentId,
    HoldStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    DateTime DbNow);
