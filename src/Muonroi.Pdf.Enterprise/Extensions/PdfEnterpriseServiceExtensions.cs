using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Pdf.Enterprise.License;

namespace Muonroi.Pdf.Enterprise.Extensions;

/// <summary>
/// Extension methods for registering PDF Enterprise services in the DI container.
/// </summary>
public static class PdfEnterpriseServiceExtensions
{
    /// <summary>
    /// Registers the real PDF Enterprise license gate: <see cref="LicenseFeatureGate"/> replaces
    /// <see cref="AlwaysAllowFeatureGate"/> as the active <see cref="IFeatureGate"/> binding.
    /// </summary>
    /// <remarks>
    /// <c>AddMEnterpriseGovernance</c> must be called first — it registers <c>ILicenseGuard</c>
    /// which <see cref="LicenseFeatureGate"/> depends on.
    /// <para>
    /// Uses <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService, TImplementation}"/>
    /// so the host can override with a test double without the no-op stub winning the binding.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPdfEnterprise(this IServiceCollection services)
    {
        // Replaces AlwaysAllowFeatureGate with the governance-backed real gate.
        // AddMEnterpriseGovernance must be called first (it registers ILicenseGuard).
        services.TryAddSingleton<IFeatureGate, LicenseFeatureGate>();
        return services;
    }
}
