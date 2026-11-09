using MediatR;

namespace SchoolAdmission.Application.Classes.ListClasses;

public sealed record ListClassesQuery : IRequest<IReadOnlyList<ClassDto>>;
