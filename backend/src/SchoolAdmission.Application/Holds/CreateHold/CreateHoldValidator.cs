using FluentValidation;
using SchoolAdmission.Application.Validation;

namespace SchoolAdmission.Application.Holds.CreateHold;

public sealed class CreateHoldValidator : AbstractValidator<CreateHoldCommand>
{
    public CreateHoldValidator() => RuleFor(x => x.StudentId).ValidStudentId();
}
