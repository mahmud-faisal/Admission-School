using MediatR;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Holds.ConfirmHold;

public sealed record ConfirmHoldCommand(string HoldId, string? StudentId)
    : IRequest<Result<HoldConfirmedDto>>, IStudentScopedRequest;
