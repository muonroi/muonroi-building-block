using Muonroi.Governance.Policy;
using System.Net.Http.Json;
using Muonroi.Governance.Abstractions.Integrity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Governance.License;

public static class LicenseServiceCollectionExtensions
{
    /// <summary>
    /// Adds OSS license protection to the application.
    /// Enterprise-grade anti-tampering/audit services are registered by Muonroi.Governance.Enterprise.
    /// </summary>
    public static IServiceCollection AddLicenseProtection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        LicenseConfigs configs = new();
        configuration.GetSection(LicenseConfigs.SectionName).Bind(configs);
        if (string.IsNullOrWhiteSpace(configs.ChainFilePath))
        {
            configs.ChainFilePath = Path.Combine("logs", "license-chain.log");
        }

        services.TryAddSingleton(configs);

        if (!string.IsNullOrWhiteSpace(configs.Online.Endpoint))
        {
            services.AddHttpClient("LicenseServer", client =>
            {
                client.BaseAddress = new Uri(configs.Online.Endpoint);
                client.Timeout = TimeSpan.FromSeconds(configs.Online.TimeoutSeconds > 0 ? configs.Online.TimeoutSeconds : 30);
            });
        }

        services.TryAddSingleton<IPolicyStore, FilePolicyStore>();
        services.TryAddSingleton<PolicyVerifier>();

        services.TryAddSingleton<LicenseStore>();
        services.TryAddSingleton<LicenseVerifier>();
        services.TryAddSingleton<LicenseRuntimeStatus>();
        services.TryAddSingleton<ProofTierAccessor>();
        services.TryAddSingleton<IAssemblyHashCollector, AssemblyHashCollector>();
        services.TryAddSingleton<ILicenseFingerprintProvider, DefaultLicenseFingerprintProvider>();
        services.TryAddSingleton<LicenseStateNotifier>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, LicenseConfigurationValidationHostedService>());

        services.TryAddSingleton(sp =>
        {
            LicenseConfigs cfg = sp.GetRequiredService<LicenseConfigs>();
            LicenseStore store = sp.GetRequiredService<LicenseStore>();
            LicenseVerifier verifier = sp.GetRequiredService<LicenseVerifier>();
            ILicenseFingerprintProvider fpProvider = sp.GetRequiredService<ILicenseFingerprintProvider>();
            LicenseRuntimeStatus runtimeStatus = sp.GetRequiredService<LicenseRuntimeStatus>();
            string fingerprint = fpProvider.GetFingerprint();
            ILoggerFactory? loggerFactory = sp.GetService<ILoggerFactory>();
            ILogger<LicenseState>? logger = loggerFactory?.CreateLogger<LicenseState>();
            ActivationProof? activationProof = store.LoadActivationProof();

            LicensePayload? payload = null;
            if (cfg.Mode == LicenseMode.Online && !string.IsNullOrWhiteSpace(cfg.Online.Endpoint))
            {
                payload = ValidateOnline(cfg, fingerprint, logger);
            }

            payload ??= store.Load();
            LicenseState state = verifier.VerifyAsync(payload, activationProof, fingerprint).GetAwaiter().GetResult();
            runtimeStatus.InitializeFromProof(state.ActivationProof);
            return state;
        });

        services.TryAddSingleton<IFingerprintChainStore>(_ => new NoopFingerprintChainStore());
        services.TryAddSingleton<IFingerprintSigner>(_ => new NoopFingerprintSigner());
        services.TryAddSingleton<ILicenseGuardEnhancer, NoopLicenseGuardEnhancer>();

        services.TryAddScoped<ILicenseGuard, LicenseGuard>();
        services.TryAddScoped<ITenantLicenseFeatureGate, TenantLicenseFeatureGateAdapter>();

        services.TryAddSingleton<ILicenseActivationService>(sp =>
        {
            LicenseConfigs cfg = sp.GetRequiredService<LicenseConfigs>();
            ILicenseFingerprintProvider fpProvider = sp.GetRequiredService<ILicenseFingerprintProvider>();
            IMJsonSerializeService jsonSerializeService = sp.GetRequiredService<IMJsonSerializeService>();
            IAssemblyHashCollector hashCollector = sp.GetRequiredService<IAssemblyHashCollector>();
            IHostEnvironment? env = sp.GetService<IHostEnvironment>();
            return new LicenseActivationService(cfg, fpProvider, jsonSerializeService, hashCollector, env);
        });

        if (configs.Mode == LicenseMode.Online && !string.IsNullOrWhiteSpace(configs.Online.Endpoint))
        {
            services.AddHostedService<LicenseRefreshHostedService>();
        }

        return services;
    }

    private static LicensePayload? ValidateOnline(LicenseConfigs configs, string fingerprint,
        ILogger<LicenseState>? logger)
    {
        try
        {
            using HttpClient client = new();
            client.Timeout = TimeSpan.FromSeconds(configs.Online.TimeoutSeconds > 0 ? configs.Online.TimeoutSeconds : 10);

            LicensePayload? existingLicense = LoadLocalLicense(configs);
            if (existingLicense?.LicenseId == null || existingLicense.LicenseId == "FREE")
            {
                return null;
            }

            var request = new
            {
                existingLicense.LicenseId,
                Fingerprint = fingerprint,
                configs.ProjectSeed,
                existingLicense.ServerNonce
            };

            HttpResponseMessage response = client.PostAsJsonAsync($"{configs.Online.Endpoint}/validate", request).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                logger?.LogDebug("[License] Online validation successful.");
                return response.Content.ReadFromJsonAsync<LicensePayload>().GetAwaiter().GetResult();
            }

            logger?.LogWarning("[License] Online validation failed: {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger?.LogWarning("[License] Online validation error: {Message}. Using cached license.", ex.Message);
        }

        return null;
    }

    private static LicensePayload? LoadLocalLicense(LicenseConfigs configs)
    {
        string? path = configs.LicenseFilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!Path.IsPathRooted(path))
        {
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        }

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LicensePayload>(json); // MBB002-exempt: static-class boundary
        }
        catch
        {
            return null;
        }
    }
}
