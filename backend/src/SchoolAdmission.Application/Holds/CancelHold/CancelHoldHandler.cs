using MediatR;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.Application.Holds.CancelHold;

/// <summary>
/// DESIGN §4.3. One conditional UPDATE (HELD, not expired, owned by the student) → CANCELLED.
/// Cancelling an already-cancelled hold is a no-op success; CONFIRMED and EXPIRED holds cannot be cancelled.
/// </summary>
public sealed class CancelHoldHandler(IHoldRepository holds) : IRequestHandler<CancelHoldCommand, Result>
{
    private const int MaxAttempts = 2;

    public async Task<Result> Handle(CancelHoldCommand request, CancellationToken cancellationToken)
    {
        var studentId = request.StudentId!; // validated

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (await holds.TryCancelAsync(request.HoldId, studentId, cancellationToken))
            {
                return Result.Success();
            }

            var snapshot = await holds.GetSnapshotAsync(request.HoldId, cancellationToken);
            switch (HoldRules.ClassifyCancel(snapshot, studentId))
            {
                case CancelOutcome.NotFound:
                    return Errors.HoldNotFound(request.HoldId);
                case CancelOutcome.Forbidden:
                    return Errors.Forbidden;
                case CancelOutcome.AlreadyCancelled:
                    return Result.Success();
                case CancelOutcome.AlreadyConfirmed:
                    return Errors.HoldAlreadyConfirmed;
                case CancelOutcome.Expired:
                    return Errors.HoldExpired;
                case CancelOutcome.StillHeld:
                    continue; // lost a race; try the UPDATE again
            }
        }

        return Errors.ConcurrentUpdate;
    }
}
