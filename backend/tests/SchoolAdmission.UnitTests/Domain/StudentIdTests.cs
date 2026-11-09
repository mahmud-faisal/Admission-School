using SchoolAdmission.Domain.Students;

namespace SchoolAdmission.UnitTests.Domain;

public class StudentIdTests
{
    [Fact]
    public void TryCreate_Accepts17AsciiDigits()
    {
        StudentId.TryCreate("20152691234567890", out var id).Should().BeTrue();
        id.Value.Should().Be("20152691234567890");
    }

    [Theory]
    [InlineData("2015269123456789")]   // 16 digits
    [InlineData("201526912345678901")] // 18 digits
    public void TryCreate_Rejects16Or18Digits(string raw) =>
        StudentId.TryCreate(raw, out _).Should().BeFalse();

    [Theory]
    [InlineData("2015269123456789a")]
    [InlineData("201526912345 7890")]
    [InlineData(" 2015269123456789")]
    [InlineData("٢٠١٥٢٦٩١٢٣٤٥٦٧٨٩٠")] // 17 Arabic-Indic digits
    public void TryCreate_RejectsLettersSpacesAndUnicodeDigits(string raw) =>
        StudentId.TryCreate(raw, out _).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryCreate_RejectsNullOrEmpty(string? raw) =>
        StudentId.TryCreate(raw, out _).Should().BeFalse();
}
