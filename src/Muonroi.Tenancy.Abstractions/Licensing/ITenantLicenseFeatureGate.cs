namespace Muonroi.Tenancy.Abstractions.Licensing;

public interface ITenantLicenseFeatureGate
{
    bool HasFeature(string featureName);
}
