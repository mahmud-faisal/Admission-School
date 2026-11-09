using MediatR;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Holds.GetMyHolds;

public sealed record GetMyHoldsQuery(string? StudentId)
    : IRequest<Result<IReadOnlyList<MyHoldDto>>>, IStudentScopedRequest;
