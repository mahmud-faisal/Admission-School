using MediatR;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Holds.GetMyHolds;

public sealed class GetMyHoldsHandler(IHoldRepository holds)
    : IRequestHandler<GetMyHoldsQuery, Result<IReadOnlyList<MyHoldDto>>>
{
    public async Task<Result<IReadOnlyList<MyHoldDto>>> Handle(GetMyHoldsQuery request, CancellationToken cancellationToken)
    {
        var rows = await holds.ListByStudentAsync(request.StudentId!, cancellationToken);
        IReadOnlyList<MyHoldDto> items = rows.Select(MyHoldDto.From).ToList();
        return Result.Success(items);
    }
}
