using SchoolAdmission.Application.Classes.CreateClass;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Holds.CreateHold;
using SchoolAdmission.Application.Holds.GetMyHolds;

namespace SchoolAdmission.UnitTests.Application.Validators;

public class ValidatorTests
{
    private readonly CreateClassValidator _createClass = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateClassValidator_RejectsBlankName(string? name) =>
        _createClass.Validate(new CreateClassCommand(name, 10)).Errors
            .Should().ContainSingle(e => e.PropertyName == "name");

    [Fact]
    public void CreateClassValidator_RejectsNameOver200() =>
        _createClass.Validate(new CreateClassCommand(new string('a', 201), 10)).IsValid.Should().BeFalse();

    [Fact]
    public void CreateClassValidator_Accepts200CharNameWithSurroundingSpaces() =>
        _createClass.Validate(new CreateClassCommand("  " + new string('a', 200) + "  ", 10)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public void CreateClassValidator_RejectsSeatsBelow1OrAbove100000(int? seats) =>
        _createClass.Validate(new CreateClassCommand("Class 6", seats)).Errors
            .Should().Contain(e => e.PropertyName == "totalSeats");

    [Theory]
    [InlineData(1)]
    [InlineData(100_000)]
    public void CreateClassValidator_AcceptsSeatBounds(int seats) =>
        _createClass.Validate(new CreateClassCommand("Class 6", seats)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("2015269123456789")]
    [InlineData("201526912345678901")]
    [InlineData("2015269123456789x")]
    public void CreateHoldValidator_RejectsBadStudentId_WithCodeInvalidStudentId(string? studentId) =>
        new CreateHoldValidator().Validate(new CreateHoldCommand("c_1", studentId)).Errors
            .Should().ContainSingle().Which.ErrorCode.Should().Be(ErrorCodes.InvalidStudentId);

    [Fact]
    public void GetMyHoldsValidator_RequiresStudentId()
    {
        var validator = new GetMyHoldsValidator();
        validator.Validate(new GetMyHoldsQuery(null)).IsValid.Should().BeFalse();
        validator.Validate(new GetMyHoldsQuery("20152691234567890")).IsValid.Should().BeTrue();
    }
}
