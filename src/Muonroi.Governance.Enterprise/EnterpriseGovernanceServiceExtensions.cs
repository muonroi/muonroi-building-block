using Muonroi.Governance.Compliance;
using Muonroi.Governance.Enterprise.Compliance;
using Muonroi.Governance.Enterprise.License;
using Muonroi.Governance.Enterprise.Operations;
using Muonroi.Governance.Enterprise.Policy;
using Muonroi.Governance.Enterprise.ServerValidation;
using Muonroi.Governance.Operations;
using Muonroi.Governance.ServerValidation;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Enterprise;

public static class EnterpriseGovernanceServiceExtensions
{
    /// <summary>
    /// Registers enterprise-grade governance: anti-tamper, chain audit, fail-closed policy.
    /// </summary>
    public static IServiceCollection AddMEnterpriseGovernance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddLicenseProtection(configuration);

        services.TryAddSingleton<PolicyEnforcer>(sp =>
        {
            IPolicyStore store = sp.GetRequiredService<IPolicyStore>();
            PolicyVerifier verifier = sp.GetRequiredService<PolicyVerifier>();
            IMDateTimeService dateTimeService = sp.GetRequiredService<IMDateTimeService>();
            IMLog<PolicyEnforcer>? logger = sp.GetService<IMLog<PolicyEnforcer>>();
            LicensePolicy? policy = store.Load();

            if (policy is not null && !verifier.Verify(policy))
            {
                logger?.Warn("[Policy] Enterprise policy signature verification failed. Enforcement disabled.");
                policy = null;
            }

            return new PolicyEnforcer(policy, dateTimeService, logger);
        });

        services.Replace(ServiceDescriptor.Singleton<ILicenseGuardEnhancer>(sp =>
        {
            LicenseConfigs configs = sp.GetRequiredService<LicenseConfigs>();
            PolicyEnforcer? policyEnforcer = sp.GetService<PolicyEnforcer>();
            return new EnterpriseLicenseGuardEnhancer(configs, policyEnforcer);
        }));

        services.Replace(ServiceDescriptor.Singleton<IFingerprintChainStore>(sp =>
        {
            IHostEnvironment? env = sp.GetService<IHostEnvironment>();
            LicenseConfigs cfg = sp.GetRequiredService<LicenseConfigs>();
            IMJsonSerializeService jsonSerializeService = sp.GetRequiredService<IMJsonSerializeService>();
            FileFingerprintChainStore store = new(env, cfg, jsonSerializeService);
            _ = store.GetLastSignature();
            return store;
        }));

        services.Replace(ServiceDescriptor.Singleton<IFingerprintSigner>(sp =>
        {
            LicenseState state = sp.GetRequiredService<LicenseState>();
            LicenseConfigs cfg = sp.GetRequiredService<LicenseConfigs>();
            return new HmacFingerprintSigner(state.Payload, cfg);
        }));

        services.Replace(ServiceDescriptor.Singleton<ILicenseFingerprintProvider>(sp =>
        {
            LicenseConfigs cfg = sp.GetRequiredService<LicenseConfigs>();
            IHostEnvironment? env = sp.GetService<IHostEnvironment>();
            return new FingerprintProvider(cfg, env);
        }));

        services.TryAddSingleton<LicenseActivator>();

        services.TryAddSingleton<TpmAnchor>();
        services.TryAddSingleton<ChainSubmitter>();
        services.TryAddSingleton<NonceRotator>();
        services.TryAddSingleton<IMComplianceExportService, MComplianceExportService>();
        services.TryAddSingleton<IMComplianceEvidencePackService, MComplianceEvidencePackService>();
        services.TryAddSingleton<IMUpgradeCompatibilityService, MUpgradeCompatibilityService>();
        services.TryAddSingleton<IMEnterpriseSloPresetService, MEnterpriseSloPresetService>();

        LicenseConfigs configs = configuration.GetSection(LicenseConfigs.SectionName).Get<LicenseConfigs>() ?? new LicenseConfigs();
        if (configs.EnableServerValidation || configs.FallbackToOnlineActivation)
        {
            services.AddHostedService<ChainSubmissionHostedService>();
        }

        if (configs.Compliance.Enabled && configs.Compliance.EnableBackgroundExport)
        {
            services.AddHostedService<MComplianceExportHostedService>();
        }

        services.AddOpenTelemetry()
            .WithTracing(t =>
            {
                t.AddSource(AntiTamperingRuntimeTelemetry.ActivitySourceName);
                t.AddSource(AuditTrailRuntimeTelemetry.ActivitySourceName);
            })
            .WithMetrics(m =>
            {
                m.AddMeter(AntiTamperingRuntimeTelemetry.MeterName);
                m.AddMeter(AuditTrailRuntimeTelemetry.MeterName);
            });

        return services;
    }
}
