using FluentValidation;
using SchoolAdmission.Domain.Classes;

namespace SchoolAdmission.Application.Classes.CreateClass;

public sealed class CreateClassValidator : AbstractValidator<CreateClassCommand>
{
    public CreateClassValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n))
            .WithMessage("Class name is required.")
            .Must(n => n is null || n.Trim().Length <= Class.MaxNameLength)
            .WithMessage($"Class name must be at most {Class.MaxNameLength} characters.")
            .OverridePropertyName("name");

        RuleFor(x => x.TotalSeats)
            .NotNull()
            .WithMessage("Total seats is required.")
            .InclusiveBetween(Class.MinSeats, Class.MaxSeats)
            .WithMessage($"Total seats must be a whole number from {Class.MinSeats} to {Class.MaxSeats}.")
            .OverridePropertyName("totalSeats");
    }
}
