namespace Muonroi.Tenancy.Abstractions.Licensing;

/// <summary>
/// Provides access to tenant license feature checks.
/// </summary>
public interface ITenantLicenseFeatureGate
{
    /// <summary>
    /// Determines whether the tenant has the specified feature enabled.
    /// </summary>
    /// <param name="featureName">The feature name to check.</param>
    /// <returns><c>true</c> if the feature is enabled; otherwise, <c>false</c>.</returns>
    bool HasFeature(string featureName);
}
