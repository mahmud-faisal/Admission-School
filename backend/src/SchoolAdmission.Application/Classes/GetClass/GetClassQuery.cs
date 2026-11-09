using MediatR;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Classes.GetClass;

/// <summary>No validator: any unknown or malformed id is simply 404 NOT_FOUND.</summary>
public sealed record GetClassQuery(string ClassId) : IRequest<Result<ClassDto>>;
