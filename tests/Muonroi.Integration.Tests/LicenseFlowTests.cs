using FluentAssertions;
using Moq;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;
namespace Muonroi.Integration.Tests;



public class LicenseFlowTests
{
    [Fact]
    public void EnterpriseLicense_ShouldEnableAllFeatures()
    {
        // Arrange
        LicenseConfigs configs = new() { FailMode = LicenseFailMode.Hard };
        LicensePayload payload = new()
        {
            LicenseId = "ENT-001",
            AllowedFeatures = new[] { "*" }
        };
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Enterprise,
            Payload = payload
        };

        Mock<IFingerprintChainStore> chainStoreMock = new();
        Mock<IFingerprintSigner> signerMock = new();

        LicenseGuard guard = new(configs, state, chainStoreMock.Object, signerMock.Object);

        // Act & Assert

        // Should not throw for any feature
        Action act1 = () => guard.EnsureFeature("rule-engine");
        Action act2 = () => guard.EnsureFeature("any-feature");

        act1.Should().NotThrow();
        act2.Should().NotThrow();
    }

    [Fact]
    public void FreeLicense_ShouldRestrictPremiumFeatures()
    {
        // Arrange
        LicenseConfigs configs = new() { FailMode = LicenseFailMode.Hard };
        LicenseState state = LicenseState.CreateFree();

        Mock<IFingerprintChainStore> chainStoreMock = new();
        Mock<IFingerprintSigner> signerMock = new();

        LicenseGuard guard = new(configs, state, chainStoreMock.Object, signerMock.Object);

        // Act & Assert

        // DistributedCache is premium
        Action act = () => guard.EnsureFeature(FreeTierFeatures.Premium.DistributedCache);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*not available*");
    }

    [Fact]
    public void InvalidLicense_WithHardFail_ShouldThrow()
    {
        // Arrange
        LicenseConfigs configs = new() { FailMode = LicenseFailMode.Hard };
        // Feature must be allowed but IsValid must be false to trigger SEC_ERR_01
        LicensePayload payload = new()
        {
            LicenseId = "LIC-001",
            AllowedFeatures = new[] { "some-action" }
        };
        LicenseState state = new() { IsValid = false, Tier = LicenseTier.Licensed, Payload = payload };

        Mock<IFingerprintChainStore> chainStoreMock = new();
        Mock<IFingerprintSigner> signerMock = new();

        LicenseGuard guard = new(configs, state, chainStoreMock.Object, signerMock.Object);

        // Act & Assert
        Action act = () => guard.EnsureValid("some-action");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*SEC_ERR_01*");
    }
}
