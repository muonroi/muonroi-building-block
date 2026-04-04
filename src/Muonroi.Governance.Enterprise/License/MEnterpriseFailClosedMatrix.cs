using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Governance.License;

internal enum MEnterpriseFailureReason
{
    MissingSignedPolicy = 0,
    InvalidSignedPolicy = 1,
    PolicyExpired = 2,
    EndpointTrustFailure = 3,
    CertificatePinningMisconfigured = 4,
    ServerResponseSignatureMissing = 5,
    ServerResponseSignatureInvalid = 6
}

internal static class MEnterpriseFailClosedMatrix
{
    public static bool ShouldBlock(string requestedFeature, MEnterpriseFailureReason reason)
    {
        string? capability = LicenseCapabilityResolver.ResolveCapabilityForRequest(requestedFeature);
        if (capability == null)
        {
            return false;
        }

        return reason switch
        {
            MEnterpriseFailureReason.MissingSignedPolicy => BlocksAllEnterpriseCapabilities(capability),
            MEnterpriseFailureReason.InvalidSignedPolicy => BlocksAllEnterpriseCapabilities(capability),
            MEnterpriseFailureReason.PolicyExpired => BlocksAllEnterpriseCapabilities(capability),
            MEnterpriseFailureReason.EndpointTrustFailure => BlocksRemoteAudit(capability),
            MEnterpriseFailureReason.CertificatePinningMisconfigured => BlocksRemoteAudit(capability),
            MEnterpriseFailureReason.ServerResponseSignatureMissing => BlocksRemoteAudit(capability),
            MEnterpriseFailureReason.ServerResponseSignatureInvalid => BlocksRemoteAudit(capability),
            _ => false
        };
    }

    private static bool BlocksAllEnterpriseCapabilities(string capability)
    {
        return capability.Equals(LicenseCapabilityResolver.Capabilities.CoreRuntime, StringComparison.OrdinalIgnoreCase) ||
               capability.Equals(LicenseCapabilityResolver.Capabilities.AuthRbacPlus, StringComparison.OrdinalIgnoreCase) ||
               capability.Equals(LicenseCapabilityResolver.Capabilities.TenancyStrict, StringComparison.OrdinalIgnoreCase) ||
               capability.Equals(LicenseCapabilityResolver.Capabilities.RulesRuntime, StringComparison.OrdinalIgnoreCase) ||
               capability.Equals(LicenseCapabilityResolver.Capabilities.TransportGrpc, StringComparison.OrdinalIgnoreCase) ||
               capability.Equals(LicenseCapabilityResolver.Capabilities.TransportMessageBus, StringComparison.OrdinalIgnoreCase) ||
               capability.Equals(LicenseCapabilityResolver.Capabilities.CacheDistributed, StringComparison.OrdinalIgnoreCase) ||
               capability.Equals(LicenseCapabilityResolver.Capabilities.AuditTrail, StringComparison.OrdinalIgnoreCase) ||
               capability.Equals(LicenseCapabilityResolver.Capabilities.RuntimeAntiTampering, StringComparison.OrdinalIgnoreCase) ||
               capability.Equals(LicenseCapabilityResolver.Capabilities.AuditRemote, StringComparison.OrdinalIgnoreCase);
    }

    private static bool BlocksRemoteAudit(string capability)
    {
        return capability.Equals(LicenseCapabilityResolver.Capabilities.AuditRemote, StringComparison.OrdinalIgnoreCase);
    }
}
