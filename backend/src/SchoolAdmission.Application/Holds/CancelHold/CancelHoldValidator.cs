using FluentValidation;
using SchoolAdmission.Application.Validation;

namespace SchoolAdmission.Application.Holds.CancelHold;

public sealed class CancelHoldValidator : AbstractValidator<CancelHoldCommand>
{
    public CancelHoldValidator() => RuleFor(x => x.StudentId).ValidStudentId();
}
