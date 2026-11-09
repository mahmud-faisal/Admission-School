using MediatR;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Holds.CancelHold;

public sealed record CancelHoldCommand(string HoldId, string? StudentId) : IRequest<Result>, IStudentScopedRequest;
