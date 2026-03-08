namespace Muonroi.Integration.Tests;

using System;
using Muonroi.Governance.License;
using FluentAssertions;
using Xunit;
using Moq;

public class LicenseFlowTests
{
    [Fact]
    public void EnterpriseLicense_ShouldEnableAllFeatures()
    {
        // Arrange
        var configs = new LicenseConfigs { FailMode = LicenseFailMode.Hard };
        var payload = new LicensePayload 
        { 
            LicenseId = "ENT-001",
            AllowedFeatures = new[] { "*" } 
        };
        var state = new LicenseState 
        { 
            IsValid = true, 
            Tier = LicenseTier.Enterprise, 
            Payload = payload 
        };
        
        var chainStoreMock = new Mock<IFingerprintChainStore>();
        var signerMock = new Mock<IFingerprintSigner>();
        
        var guard = new LicenseGuard(configs, state, chainStoreMock.Object, signerMock.Object);

        // Act & Assert
        
        // Should not throw for any feature
        var act1 = () => guard.EnsureFeature("rule-engine");
        var act2 = () => guard.EnsureFeature("any-feature");
        
        act1.Should().NotThrow();
        act2.Should().NotThrow();
    }

    [Fact]
    public void FreeLicense_ShouldRestrictPremiumFeatures()
    {
        // Arrange
        var configs = new LicenseConfigs { FailMode = LicenseFailMode.Hard };
        var state = LicenseState.CreateFree();
        
        var chainStoreMock = new Mock<IFingerprintChainStore>();
        var signerMock = new Mock<IFingerprintSigner>();
        
        var guard = new LicenseGuard(configs, state, chainStoreMock.Object, signerMock.Object);

        // Act & Assert
        
        // DistributedCache is premium
        var act = () => guard.EnsureFeature(FreeTierFeatures.Premium.DistributedCache);
        
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*not available*");
    }

    [Fact]
    public void InvalidLicense_WithHardFail_ShouldThrow()
    {
        // Arrange
        var configs = new LicenseConfigs { FailMode = LicenseFailMode.Hard };
        // Feature must be allowed but IsValid must be false to trigger SEC_ERR_01
        var payload = new LicensePayload 
        { 
            LicenseId = "LIC-001",
            AllowedFeatures = new[] { "some-action" } 
        };
        var state = new LicenseState { IsValid = false, Tier = LicenseTier.Licensed, Payload = payload };
        
        var chainStoreMock = new Mock<IFingerprintChainStore>();
        var signerMock = new Mock<IFingerprintSigner>();
        
        var guard = new LicenseGuard(configs, state, chainStoreMock.Object, signerMock.Object);

        // Act & Assert
        var act = () => guard.EnsureValid("some-action");
        
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*SEC_ERR_01*");
    }
}
