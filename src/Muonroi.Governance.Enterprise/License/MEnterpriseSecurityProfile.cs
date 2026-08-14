namespace Muonroi.Governance.License;

internal static class MEnterpriseSecurityProfile
{
    public static bool IsSecureDefaultsActive(LicenseConfigs configs, LicenseState state)
    {
        if (!configs.Enterprise.EnableSecureDefaults)
        {
            return false;
        }

        if (state.Tier != LicenseTier.Enterprise)
        {
            return false;
        }

        return configs.GetEffectiveEnforcementMode(state.Tier) == LicenseEnforcementMode.Production;
    }

    public static bool IsSignedPolicyRequired(LicenseConfigs configs, LicenseState state)
    {
        if (configs.RequireSignedPolicy)
        {
            return true;
        }

        return IsSecureDefaultsActive(configs, state) &&
               !configs.Enterprise.AllowPolicyBypassInProduction;
    }

    public static bool IsEndpointTrustFailClosed(LicenseConfigs configs, LicenseState state)
    {
        return IsSecureDefaultsActive(configs, state) &&
               !configs.Enterprise.AllowEndpointTrustBypassInProduction;
    }

    public static bool RequiresCertificatePinning(LicenseConfigs configs, LicenseState state)
    {
        return IsEndpointTrustFailClosed(configs, state) &&
               configs.Enterprise.RequireCertificatePinningInProduction;
    }

    public static bool RequiresTrustedEndpoint(LicenseConfigs configs, LicenseState state)
    {
        return IsEndpointTrustFailClosed(configs, state) &&
               configs.Enterprise.RequireTrustedEndpointInProduction;
    }

    public static bool RequiresServerResponseSignature(LicenseConfigs configs, LicenseState state)
    {
        return IsEndpointTrustFailClosed(configs, state) &&
               configs.Enterprise.RequireServerResponseSignatureInProduction;
    }

    public static bool HasPinningMaterial(LicenseConfigs configs)
    {
        if (!configs.Online.EnableCertificatePinning)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(configs.Online.ExpectedCertificateThumbprint))
        {
            return true;
        }

        return configs.Online.TrustedCertificateThumbprints?.Any(x => !string.IsNullOrWhiteSpace(x)) == true;
    }

    public static bool IsTrustedLicenseServerHost(LicenseConfigs configs, string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        string normalized = host.Trim().ToLowerInvariant();
        string[] configured = configs.Enterprise.TrustedLicenseServerHosts ?? [];
        if (configured.Length == 0)
        {
            return false;
        }

        return configured.Any(x => string.Equals(x?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }
}
