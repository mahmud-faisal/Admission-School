using MediatR;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Holds.GetHold;

/// <summary>No X-Student-Id required (ASSUMPTIONS). Unknown ids are 404, so no validator is needed.</summary>
public sealed record GetHoldQuery(string HoldId) : IRequest<Result<HoldDto>>;
