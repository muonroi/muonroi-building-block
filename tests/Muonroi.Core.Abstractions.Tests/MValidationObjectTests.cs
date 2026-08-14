namespace Muonroi.Core.Abstractions.Tests;

public class MValidationObjectTests
{
    private sealed class ValidModel : MValidationObject
    {
        [Required]
        public string Name { get; set; } = "Test";
    }

    private sealed class InvalidModel : MValidationObject
    {
        [Required(ErrorMessage = "NameRequired")]
        public string? Name { get; set; }

        [Range(1, 100, ErrorMessage = "AgeRange")]
        public int Age { get; set; }
    }

    [Fact]
    public void IsValid_ReturnsTrue_WhenModelIsValid()
    {
        ValidModel model = new();

        bool result = model.IsValid();

        result.Should().BeTrue();
        model.ErrorMessages.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenRequiredFieldMissing()
    {
        InvalidModel model = new() { Name = null, Age = 50 };

        bool result = model.IsValid();

        result.Should().BeFalse();
        model.ErrorMessages.Should().NotBeEmpty();
        model.ErrorMessages.Should().Contain(e => e.ErrorCode == "NameRequired");
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenRangeViolated()
    {
        InvalidModel model = new() { Name = "Test", Age = 200 };

        bool result = model.IsValid();

        result.Should().BeFalse();
        model.ErrorMessages.Should().Contain(e => e.ErrorCode == "AgeRange");
    }

    [Fact]
    public void IsValid_ClearsOldErrors_OnSecondCall()
    {
        InvalidModel model = new() { Name = null, Age = 50 };

        model.IsValid();
        int firstCount = model.ErrorMessages.Count;

        model.Name = "Valid";
        model.IsValid();

        model.ErrorMessages.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_MultipleViolations_ReportsAll()
    {
        InvalidModel model = new() { Name = null, Age = 0 };

        bool result = model.IsValid();

        result.Should().BeFalse();
        model.ErrorMessages.Count.Should().BeGreaterThanOrEqualTo(2);
    }
}
