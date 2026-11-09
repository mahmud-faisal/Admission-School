using FluentValidation;
using SchoolAdmission.Application.Validation;

namespace SchoolAdmission.Application.Holds.GetMyHolds;

public sealed class GetMyHoldsValidator : AbstractValidator<GetMyHoldsQuery>
{
    public GetMyHoldsValidator() => RuleFor(x => x.StudentId).ValidStudentId();
}
