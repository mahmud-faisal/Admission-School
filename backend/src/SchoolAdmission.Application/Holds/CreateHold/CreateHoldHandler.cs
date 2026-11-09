using MediatR;
using Microsoft.Extensions.Options;
using SchoolAdmission.Application.Abstractions;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Common.Options;

namespace SchoolAdmission.Application.Holds.CreateHold;

/// <summary>
/// DESIGN §4.1. Everything happens in one READ COMMITTED transaction that first locks the class row
/// (<c>SELECT ... FOR UPDATE</c>). The lock lives in PostgreSQL, so hold creation for one class is serialized
/// across every backend process. There is deliberately no in-process lock here.
/// </summary>
public sealed class CreateHoldHandler(
    IClassRepository classes,
    IHoldRepository holds,
    IUnitOfWork unitOfWork,
    IIdGenerator ids,
    IOptions<HoldOptions> options)
    : IRequestHandler<CreateHoldCommand, Result<HoldCreatedDto>>
{
    public async Task<Result<HoldCreatedDto>> Handle(CreateHoldCommand request, CancellationToken cancellationToken)
    {
        var studentId = request.StudentId!; // validated: 17 digits
        var classId = request.ClassId;

        await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // 1+2. Lock the class row; DB clock read AFTER the lock is held.
        var locked = await classes.LockForHoldCreationAsync(classId, cancellationToken);
        if (locked is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Errors.ClassNotFound(classId);
        }

        var now = locked.DbNow;

        // 3. Stale HELD rows leave the partial unique index so re-applying after expiry works.
        await holds.ExpireStaleForClassAsync(classId, now, cancellationToken);

        // 4. Duplicate check before the seat check (gives the more useful message).
        if (await holds.HasActiveOrConfirmedAsync(classId, studentId, now, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            return Errors.DuplicateApplication;
        }

        // 5. Seat check under the lock: a fresh READ COMMITTED snapshot sees every committed hold.
        var occupied = await holds.CountOccupiedSeatsAsync(classId, now, cancellationToken);
        if (occupied >= locked.TotalSeats)
        {
            await tx.RollbackAsync(cancellationToken);
            return Errors.SeatsFull;
        }

        // 6. Insert. The unique index is defence in depth; step 4 under the lock should make it unreachable.
        HoldRow row;
        try
        {
            row = await holds.InsertHeldAsync(
                ids.NewHoldId(), classId, studentId, now, options.Value.HoldDurationSeconds, cancellationToken);
        }
        catch (DuplicateHoldException)
        {
            await tx.RollbackAsync(cancellationToken);
            return Errors.DuplicateApplication;
        }

        await tx.CommitAsync(cancellationToken);

        return new HoldCreatedDto(
            row.Id, row.ClassId, row.StudentId, row.Status, row.ExpiresAt, locked.Name, row.CreatedAt, now);
    }
}
