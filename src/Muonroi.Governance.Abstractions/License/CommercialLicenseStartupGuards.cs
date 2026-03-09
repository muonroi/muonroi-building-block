using Microsoft.Extensions.Hosting;

namespace Muonroi.Governance.License;

public static class CommercialLicenseStartupGuards
{
    public static IServiceCollection RequireMinimumTierFromProof(
        this IServiceCollection services,
        LicenseTier minimumTier,
        string featureName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(featureName);

        services.AddSingleton<IHostedService>(sp =>
        {
            LicenseState? state = sp.GetService<LicenseState>();
            if (state == null)
            {
                throw new LicenseException(
                    $"[LICENSE] LicenseState is not registered. Call AddLicenseProtection before enabling feature '{featureName}'.");
            }

            return new CommercialFeatureStartupValidationHostedService(
                state,
                sp.GetService<LicenseRuntimeStatus>() ?? new LicenseRuntimeStatus(),
                minimumTier,
                featureName);
        });
        return services;
    }
}
