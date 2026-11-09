using MediatR;
using SchoolAdmission.Application.Abstractions.Persistence;

namespace SchoolAdmission.Application.Classes.ListClasses;

public sealed class ListClassesHandler(IClassRepository classes)
    : IRequestHandler<ListClassesQuery, IReadOnlyList<ClassDto>>
{
    public async Task<IReadOnlyList<ClassDto>> Handle(ListClassesQuery request, CancellationToken cancellationToken)
    {
        var rows = await classes.ListSeatsAsync(cancellationToken);
        return rows.Select(ClassDto.From).ToList();
    }
}
