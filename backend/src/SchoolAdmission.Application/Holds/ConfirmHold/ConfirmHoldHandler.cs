using MediatR;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.Application.Holds.ConfirmHold;

/// <summary>
/// DESIGN §4.2. One atomic conditional UPDATE does the confirm. HELD → CONFIRMED can happen at most once,
/// so confirmedSeats grows by exactly one however many confirms arrive. If the UPDATE matches no row,
/// a diagnostic read explains why; an already-CONFIRMED hold of the same student is a 200 (idempotent).
/// </summary>
public sealed class ConfirmHoldHandler(IHoldRepository holds)
    : IRequestHandler<ConfirmHoldCommand, Result<HoldConfirmedDto>>
{
    private const int MaxAttempts = 2;

    public async Task<Result<HoldConfirmedDto>> Handle(ConfirmHoldCommand request, CancellationToken cancellationToken)
    {
        var studentId = request.StudentId!; // validated

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var updated = await holds.TryConfirmAsync(request.HoldId, studentId, cancellationToken);
            if (updated is not null)
            {
                return HoldConfirmedDto.From(updated);
            }

            var snapshot = await holds.GetSnapshotAsync(request.HoldId, cancellationToken);
            var outcome = HoldRules.ClassifyConfirm(snapshot, studentId);
            switch (outcome)
            {
                case ConfirmOutcome.NotFound:
                    return Errors.HoldNotFound(request.HoldId);
                case ConfirmOutcome.Forbidden:
                    return Errors.Forbidden;
                case ConfirmOutcome.AlreadyConfirmed:
                    var view = await holds.GetViewAsync(request.HoldId, cancellationToken);
                    return view is null ? Errors.HoldNotFound(request.HoldId) : HoldConfirmedDto.From(view);
                case ConfirmOutcome.Cancelled:
                case ConfirmOutcome.Expired:
                    var confirmedElsewhere = await holds.GetConfirmedHoldAsync(studentId, cancellationToken);
                    if (confirmedElsewhere is not null && confirmedElsewhere.HoldId != request.HoldId)
                    {
                        return Errors.AlreadyAdmitted(confirmedElsewhere.ClassName);
                    }

                    return outcome == ConfirmOutcome.Cancelled ? Errors.HoldCancelled : Errors.HoldExpired;

                case ConfirmOutcome.StillHeld:
                    continue; // lost a race with another writer; try the UPDATE again
            }
        }

        return Errors.ConcurrentUpdate;
    }
}
