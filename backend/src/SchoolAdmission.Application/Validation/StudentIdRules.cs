using FluentValidation;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Domain.Students;

namespace SchoolAdmission.Application.Validation;

public static class StudentIdRules
{
    public const string PropertyName = "X-Student-Id";

    public const string Message =
        "The X-Student-Id header must be the student's 17-digit birth registration number (digits 0-9 only).";

    /// <summary>Exactly 17 ASCII digits; failure code INVALID_STUDENT_ID.</summary>
    public static IRuleBuilderOptions<T, string?> ValidStudentId<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(StudentId.IsValid)
            .WithErrorCode(ErrorCodes.InvalidStudentId)
            .WithMessage(Message)
            .OverridePropertyName(PropertyName);
}
