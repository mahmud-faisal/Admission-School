using MediatR;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Holds.CreateHold;

public sealed record CreateHoldCommand(string ClassId, string? StudentId)
    : IRequest<Result<HoldCreatedDto>>, IStudentScopedRequest;
