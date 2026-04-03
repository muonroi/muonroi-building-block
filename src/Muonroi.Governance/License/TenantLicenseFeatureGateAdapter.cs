namespace Muonroi.Governance.License;

internal sealed class TenantLicenseFeatureGateAdapter(ILicenseGuard guard) : ITenantLicenseFeatureGate
{
    public bool HasFeature(string featureName)
    {
        return guard.HasFeature(featureName);
    }
}
