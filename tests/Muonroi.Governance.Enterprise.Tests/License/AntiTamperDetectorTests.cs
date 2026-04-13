using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;
using System.Reflection;

namespace Muonroi.Governance.Enterprise.Tests.License;

public class AntiTamperDetectorTests
{
    [Fact]
    public void DetectTampering_WithDefaultConfig_ShouldNotThrow()
    {
        // Arrange
        var configs = new LicenseConfigs { EnableHardwareBreakpointDetection = false };
        var detector = new AntiTamperDetector(configs);

        // Act
        var result = detector.DetectTampering();

        // Assert - result depends on environment, but it shouldn't crash
    }

    [Fact]
    public void IsMethodHooked_WithNullMethod_ShouldReturnFalse()
    {
        // Act
        var result = AntiTamperDetector.IsMethodHooked(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMethodHooked_WithRealMethod_ShouldReturnFalseIfNotHooked()
    {
        // Arrange
        var method = typeof(AntiTamperDetectorTests).GetMethod(nameof(IsMethodHooked_WithRealMethod_ShouldReturnFalseIfNotHooked));

        // Act
        var result = AntiTamperDetector.IsMethodHooked(method!);

        // Assert
        // In a normal test environment, this shouldn't be hooked
        Assert.False(result);
    }
}
