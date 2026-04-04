using Muonroi.Governance.Operations;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Governance.Enterprise.Tests.Operations;

public class EnterpriseSloPresetServiceTests
{
    private readonly MEnterpriseSloPresetService _service = new();

    [Fact]
    public void GetPresetNames_ShouldReturnPresets()
    {
        // Act
        var names = _service.GetPresetNames();

        // Assert
        Assert.Contains("balanced", names);
        Assert.Contains("strict", names);
        Assert.Contains("regulated", names);
    }

    [Theory]
    [InlineData("balanced")]
    [InlineData("strict")]
    [InlineData("regulated")]
    [InlineData("")]
    [InlineData(null)]
    public void GetPreset_WithValidName_ShouldReturnPreset(string? name)
    {
        // Act
        var preset = _service.GetPreset(name);

        // Assert
        Assert.NotNull(preset);
        if (!string.IsNullOrEmpty(name))
        {
            Assert.Equal(name, preset.Name);
        }
    }

    [Fact]
    public void GetPreset_WithInvalidName_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<MArgumentException>(() => _service.GetPreset("invalid"));
    }
}
