using MediatR;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Classes.GetClass;

public sealed class GetClassHandler(IClassRepository classes) : IRequestHandler<GetClassQuery, Result<ClassDto>>
{
    public async Task<Result<ClassDto>> Handle(GetClassQuery request, CancellationToken cancellationToken)
    {
        var row = await classes.GetSeatsAsync(request.ClassId, cancellationToken);
        return row is null ? Errors.ClassNotFound(request.ClassId) : ClassDto.From(row);
    }
}
