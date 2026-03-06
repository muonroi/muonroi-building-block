namespace Muonroi.Governance.License;

public static class LicenseGuardExtensions
{
    /// <summary>
    /// Startup-time fail-fast guard for infrastructure package registration.
    /// Requires AddLicenseProtection to be registered beforehand.
    /// </summary>
    public static IServiceCollection EnsureFeatureOrThrow(
        this IServiceCollection services,
        string featureName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(featureName);

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = false
        });

        LicenseState? state = provider.GetService<LicenseState>();
        if (state is null)
        {
            throw new LicenseException(
                $"[LICENSE] LicenseState is not registered. Call AddLicenseProtection before enabling feature '{featureName}'.");
        }

        if (state.HasFeature(featureName))
        {
            return services;
        }

        LicenseTier minimumTier = FeatureTierMap.TryGetMinimumTier(featureName, out LicenseTier configuredTier)
            ? configuredTier
            : LicenseTier.Licensed;

        throw new LicenseException(
            $"[LICENSE] Feature '{featureName}' requires tier '{minimumTier}'. Current tier is '{state.Tier}'.");
    }
}
