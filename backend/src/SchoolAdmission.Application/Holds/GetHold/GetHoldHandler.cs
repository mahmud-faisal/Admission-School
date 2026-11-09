using MediatR;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Holds.GetHold;

public sealed class GetHoldHandler(IHoldRepository holds) : IRequestHandler<GetHoldQuery, Result<HoldDto>>
{
    public async Task<Result<HoldDto>> Handle(GetHoldQuery request, CancellationToken cancellationToken)
    {
        var row = await holds.GetViewAsync(request.HoldId, cancellationToken);
        return row is null ? Errors.HoldNotFound(request.HoldId) : HoldDto.From(row);
    }
}
