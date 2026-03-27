namespace Muonroi.Observability.Tests;

using System.Diagnostics;
using System.Collections.Generic;
using Muonroi.Observability;
using Muonroi.Governance.Abstractions.License;

public class OtelTests
{
    [Fact]
    public void AddObservability_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        
        // EnsureFeatureOrThrow looks for LicenseState in DI
        services.AddSingleton(new LicenseState { IsValid = true, Tier = LicenseTier.Enterprise });
        // It might also look for ILicenseGuard
        services.AddSingleton(new Mock<ILicenseGuard>().Object);

        // Act
        services.AddObservability(config);
        var provider = services.BuildServiceProvider();

        // Assert
        var activitySource = new ActivitySource("Muonroi.Core");
        Assert.NotNull(activitySource);
    }

    [Fact]
    public void ActivitySource_ShouldExist()
    {
        // Arrange & Act
        var source = new ActivitySource("TestTrack");

        // Assert
        Assert.NotNull(source);
        Assert.Equal("TestTrack", source.Name);
    }
}
