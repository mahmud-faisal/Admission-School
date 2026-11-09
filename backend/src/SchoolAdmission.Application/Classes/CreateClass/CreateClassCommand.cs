using MediatR;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Application.Classes.CreateClass;

/// <summary>Values are nullable so a missing JSON field becomes a 400 VALIDATION_FAILED, not a default value.</summary>
public sealed record CreateClassCommand(string? Name, int? TotalSeats) : IRequest<Result<ClassDto>>;
