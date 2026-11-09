using FluentValidation;
using SchoolAdmission.Application.Validation;

namespace SchoolAdmission.Application.Holds.ConfirmHold;

public sealed class ConfirmHoldValidator : AbstractValidator<ConfirmHoldCommand>
{
    public ConfirmHoldValidator() => RuleFor(x => x.StudentId).ValidStudentId();
}
