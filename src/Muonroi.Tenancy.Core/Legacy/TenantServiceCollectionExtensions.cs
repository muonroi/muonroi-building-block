using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Tenancy.Core.Legacy;

/// <summary>
/// Service registration helpers for legacy tenant context components.
/// </summary>
public static class TenantServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tenant context services and middleware.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional configuration for multi-tenant settings.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddTenantContext(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        bool enabled = true;
        if (configuration != null)
        {
            MultiTenantConfigs options = new();
            configuration.GetSection(MultiTenantConfigs.SectionName).Bind(options);
            services.Configure<MultiTenantConfigs>(configuration.GetSection(MultiTenantConfigs.SectionName));
            enabled = options.Enabled;
        }
        else
        {
            services.Configure<MultiTenantConfigs>(_ => { });
        }

        if (enabled)
        {
            services.GetOrCreateRegistry().Register(MCapability.MultiTenant);
            EnsureMultiTenantLicensed(services);
            services.TryAddSingleton<ITenantContext, TenantContext>();
            services.TryAddScoped<ITenantIdResolver, DefaultTenantIdResolver>();
            services.TryAddScoped(sp =>
                new TenantContextMiddleware(
                    _ => Task.CompletedTask,
                    sp.GetRequiredService<ITenantIdResolver>(),
                    sp.GetService<IMLogContext>() ?? NullMLogContext.Instance,
                    sp.GetService<ITenantLicenseFeatureGate>(),
                    sp.GetService<IOptions<MultiTenantConfigs>>()));
        }

        return services;
    }

    private static void EnsureMultiTenantLicensed(IServiceCollection services)
    {
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = false
        });

        ITenantLicenseFeatureGate? licenseFeatureGate = provider.GetService<ITenantLicenseFeatureGate>();
        MGuard.Configured(licenseFeatureGate != null,
                "[Muonroi] ITenantLicenseFeatureGate is not registered. " +
                "Call AddLicenseProtection() before AddTenantContext(). " +
                "Example:\n" +
                "  services.AddLicenseProtection(config);\n" +
                "  services.AddTenantContext(config);", "LicenseConfigs");

        if (licenseFeatureGate != null)
        {
            MGuard.State(licenseFeatureGate.HasFeature(TenantLicenseFeatures.Premium.MultiTenant),
                    "[LICENSE] Feature 'multi-tenant' is not available under the current license tier.");
        }
    }

    /// <summary>
    /// Registers a custom tenant identifier resolver.
    /// </summary>
    /// <typeparam name="Tr">The resolver type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddTenantIdResolver<Tr>(this IServiceCollection services)
        where Tr : class, ITenantIdResolver
    {
        _ = services.AddScoped<ITenantIdResolver, Tr>();
        return services;
    }

    private sealed class NullMLogContext : IMLogContext
    {
        public static readonly NullMLogContext Instance = new();

        public IMLogContextScope PushProperty(string key, object? value)
        {
            _ = key;
            _ = value;
            return NullScope.Instance;
        }

        public IMLogContextScope PushProperties(IReadOnlyDictionary<string, object?> properties)
        {
            _ = properties;
            return NullScope.Instance;
        }
    }

    private sealed class NullScope : IMLogContextScope
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
