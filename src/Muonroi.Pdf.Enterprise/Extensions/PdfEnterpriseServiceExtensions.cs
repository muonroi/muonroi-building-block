using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging.Abstractions;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Enterprise.License;
using Muonroi.Pdf.Enterprise.Metering;
using Muonroi.Quota.Abstractions;

namespace Muonroi.Pdf.Enterprise.Extensions;

/// <summary>
/// Extension methods for registering PDF Enterprise services in the DI container.
/// </summary>
public static class PdfEnterpriseServiceExtensions
{
    /// <summary>
    /// Registers PDF Enterprise services: <see cref="LicenseFeatureGate"/> as the active
    /// <see cref="IFeatureGate"/> binding, and <see cref="EnterprisePdfServiceWrapper"/> as the
    /// active <see cref="IMPdfService"/> binding (metering decorator wrapping the OSS engine).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Call order is significant:</b>
    /// <list type="number">
    /// <item><c>AddMPdf()</c> (or equivalent) must be called first — it registers the inner
    ///   <see cref="IMPdfService"/> (e.g. <c>MPdfService</c>) via <c>TryAddSingleton</c>.</item>
    /// <item><c>AddMEnterpriseGovernance()</c> must be called before this — it registers
    ///   <c>ILicenseGuard</c> which <see cref="LicenseFeatureGate"/> depends on.</item>
    /// <item><c>AddPdfEnterprise()</c> then decorates both registrations.</item>
    /// </list>
    /// </para>
    /// <para>
    /// There is no Scrutor <c>.Decorate</c> helper in this stack. The decorator is wired by hand:
    /// the prior <see cref="IMPdfService"/> descriptor is captured, removed, and replaced with a
    /// factory that constructs <see cref="EnterprisePdfServiceWrapper"/> wrapping the inner instance.
    /// If no prior <see cref="IMPdfService"/> registration is present (i.e. <c>AddMPdf</c> was not
    /// called before this extension), the wrapper registration is skipped and the extension returns
    /// without re-binding — a warning is deliberately omitted to keep DI startup noise-free; callers
    /// that forget <c>AddMPdf</c> will discover the gap at resolve time.
    /// </para>
    /// <para>
    /// Uses <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService, TImplementation}"/>
    /// for the gate so the host can override with a test double.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPdfEnterprise(this IServiceCollection services)
    {
        // Replaces AlwaysAllowFeatureGate with the governance-backed real gate.
        // AddMEnterpriseGovernance must be called first (it registers ILicenseGuard).
        services.TryAddSingleton<IFeatureGate, LicenseFeatureGate>();

        // Wire EnterprisePdfServiceWrapper as the active IMPdfService (D-02/SC3).
        // There is no Scrutor in this stack — hand-decorate by capturing the prior descriptor.
        ServiceDescriptor? innerDescriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(IMPdfService));

        if (innerDescriptor is null)
        {
            // AddMPdf was not called yet — skip wrapper registration.
            // The host will discover the missing binding at resolve time.
            return services;
        }

        // Remove the prior IMPdfService registration (e.g. MPdfService registered by AddMPdf).
        services.Remove(innerDescriptor);

        // Re-register IMPdfService as a factory that constructs the wrapper around the inner instance.
        services.AddSingleton<IMPdfService>(sp =>
        {
            // Resolve the inner IMPdfService from the captured prior descriptor.
            IMPdfService innerService = ResolveFromDescriptor(sp, innerDescriptor);

            return new EnterprisePdfServiceWrapper(
                innerService,
                sp.GetRequiredService<ITenantQuotaTracker>(),
                sp.GetService<ISystemExecutionContextAccessor>(),
                sp.GetService<IMLog<EnterprisePdfServiceWrapper>>());
        });

        return services;
    }

    /// <summary>
    /// Resolves an <see cref="IMPdfService"/> instance from a captured <see cref="ServiceDescriptor"/>
    /// without requiring the descriptor to have been resolved through the container already.
    /// </summary>
    private static IMPdfService ResolveFromDescriptor(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IMPdfService instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IMPdfService)descriptor.ImplementationFactory(sp);
        }

        if (descriptor.ImplementationType is not null)
        {
            return (IMPdfService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }

#pragma warning disable MSTD0001
        // MSTD0001 suppressed: this is a DI bootstrap invariant violation — callers without
        // AddMPdf registered have no valid inner service; InvalidOperationException is the
        // idiomatic DI startup exception type and must not carry an MException dependency.
        throw new InvalidOperationException(
            $"[PDF] Cannot resolve inner IMPdfService from captured descriptor: " +
            $"no ImplementationInstance, ImplementationFactory, or ImplementationType.");
#pragma warning restore MSTD0001
    }
}
