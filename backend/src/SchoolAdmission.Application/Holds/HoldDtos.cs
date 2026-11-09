using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.Application.Holds;

/// <summary>Response of POST /classes/{classId}/holds. Spec fields first, then extras.</summary>
public sealed record HoldCreatedDto(
    string HoldId,
    string ClassId,
    string StudentId,
    HoldStatus Status,
    DateTime ExpiresAt,
    string ClassName,
    DateTime CreatedAt,
    DateTime ServerTime);

/// <summary>Response of GET /holds/{holdId}. <see cref="Status"/> is the effective status.</summary>
public sealed record HoldDto(
    string HoldId,
    string ClassId,
    HoldStatus Status,
    DateTime ExpiresAt,
    string StudentId,
    string ClassName,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    DateTime ServerTime)
{
    public static HoldDto From(HoldRow row) =>
        new(row.Id, row.ClassId, row.Status, row.ExpiresAt, row.StudentId, row.ClassName, row.CreatedAt,
            row.ConfirmedAt, row.CancelledAt, row.DbNow);
}

/// <summary>Response of POST /holds/{holdId}/confirm.</summary>
public sealed record HoldConfirmedDto(
    string HoldId,
    HoldStatus Status,
    string ClassId,
    DateTime ExpiresAt,
    DateTime? ConfirmedAt)
{
    public static HoldConfirmedDto From(HoldRow row) =>
        new(row.Id, row.Status, row.ClassId, row.ExpiresAt, row.ConfirmedAt);
}

/// <summary>One item of GET /me/holds.</summary>
public sealed record MyHoldDto(
    string HoldId,
    string ClassId,
    string ClassName,
    HoldStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? ConfirmedAt)
{
    public static MyHoldDto From(HoldRow row) =>
        new(row.Id, row.ClassId, row.ClassName, row.Status, row.ExpiresAt, row.CreatedAt, row.ConfirmedAt);
}
