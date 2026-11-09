using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolAdmission.Application.Classes;
using SchoolAdmission.Application.Classes.CreateClass;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Common.Behaviors;

namespace SchoolAdmission.UnitTests.Application.Behaviors;

public class BehaviorTests
{
    [Fact]
    public async Task ValidationBehaviorTests_ThrowsWithAllFailures()
    {
        var behavior = new ValidationBehavior<CreateClassCommand, Result<ClassDto>>([new CreateClassValidator()]);
        var nextCalled = false;

        var act = () => behavior.Handle(new CreateClassCommand("", 0), _ =>
        {
            nextCalled = true;
            return Task.FromResult<Result<ClassDto>>(Errors.SeatsFull);
        }, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo("name", "totalSeats");
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ValidationBehavior_Valid_CallsNext()
    {
        var behavior = new ValidationBehavior<CreateClassCommand, Result<ClassDto>>([new CreateClassValidator()]);

        var result = await behavior.Handle(new CreateClassCommand("Class 6", 10),
            _ => Task.FromResult<Result<ClassDto>>(Errors.SeatsFull), CancellationToken.None);

        result.Error.Should().Be(Errors.SeatsFull);
    }

    [Theory]
    [InlineData("20152691234567890", "*************7890")]
    [InlineData(null, "(none)")]
    [InlineData("123", "***")]
    public void LoggingBehavior_MasksStudentId(string? input, string expected) =>
        LoggingBehavior<CreateClassCommand, Result<ClassDto>>.Mask(input).Should().Be(expected);

    [Fact]
    public async Task LoggingBehavior_PassesResponseThrough()
    {
        var behavior = new LoggingBehavior<CreateClassCommand, Result<ClassDto>>(
            NullLogger<LoggingBehavior<CreateClassCommand, Result<ClassDto>>>.Instance);

        var result = await behavior.Handle(new CreateClassCommand("x", 1),
            _ => Task.FromResult<Result<ClassDto>>(Errors.SeatsFull), CancellationToken.None);

        result.Error!.Code.Should().Be(ErrorCodes.SeatsFull);
    }
}
