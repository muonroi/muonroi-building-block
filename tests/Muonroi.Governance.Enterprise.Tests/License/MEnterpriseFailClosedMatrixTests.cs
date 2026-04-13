using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;

namespace Muonroi.Governance.Enterprise.Tests.License;

public class MEnterpriseFailClosedMatrixTests
{
    [Theory]
    [InlineData(LicenseCapabilityResolver.Capabilities.CoreRuntime, (int)MEnterpriseFailureReason.MissingSignedPolicy, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.AuthRbacPlus, (int)MEnterpriseFailureReason.InvalidSignedPolicy, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.TenancyStrict, (int)MEnterpriseFailureReason.PolicyExpired, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.RulesRuntime, (int)MEnterpriseFailureReason.MissingSignedPolicy, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.TransportGrpc, (int)MEnterpriseFailureReason.MissingSignedPolicy, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.TransportMessageBus, (int)MEnterpriseFailureReason.MissingSignedPolicy, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.CacheDistributed, (int)MEnterpriseFailureReason.MissingSignedPolicy, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.AuditTrail, (int)MEnterpriseFailureReason.MissingSignedPolicy, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.RuntimeAntiTampering, (int)MEnterpriseFailureReason.MissingSignedPolicy, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.AuditRemote, (int)MEnterpriseFailureReason.MissingSignedPolicy, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.Connectors, (int)MEnterpriseFailureReason.MissingSignedPolicy, false)]
    [InlineData(LicenseCapabilityResolver.Capabilities.AuditRemote, (int)MEnterpriseFailureReason.EndpointTrustFailure, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.AuditRemote, (int)MEnterpriseFailureReason.CertificatePinningMisconfigured, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.AuditRemote, (int)MEnterpriseFailureReason.ServerResponseSignatureMissing, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.AuditRemote, (int)MEnterpriseFailureReason.ServerResponseSignatureInvalid, true)]
    [InlineData(LicenseCapabilityResolver.Capabilities.CoreRuntime, (int)MEnterpriseFailureReason.EndpointTrustFailure, false)]
    [InlineData("unknown.feature", (int)MEnterpriseFailureReason.MissingSignedPolicy, false)]
    [InlineData("", (int)MEnterpriseFailureReason.MissingSignedPolicy, false)]
    public void ShouldBlock_ShouldReturnExpectedResult(string requestedFeature, int reason, bool expected)
    {
        // Act
        bool result = MEnterpriseFailClosedMatrix.ShouldBlock(requestedFeature, (MEnterpriseFailureReason)reason);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldBlock_WithNullFeature_ShouldReturnFalse()
    {
        // Act
        bool result = MEnterpriseFailClosedMatrix.ShouldBlock(null!, MEnterpriseFailureReason.MissingSignedPolicy);

        // Assert
        Assert.False(result);
    }


    [Fact]
    public void ShouldBlock_WithInvalidReason_ShouldReturnFalse()
    {
        // Act
        bool result = MEnterpriseFailClosedMatrix.ShouldBlock(LicenseCapabilityResolver.Capabilities.CoreRuntime, (MEnterpriseFailureReason)999);

        // Assert
        Assert.False(result);
    }
}

