namespace Muonroi.Tenancy.Core.Legacy;

public static class TenantServiceCollectionExtensions
{
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
        if (licenseFeatureGate is null)
        {
            throw new InvalidOperationException(
                "[LICENSE] ITenantLicenseFeatureGate is not registered. Call AddLicenseProtection before AddTenantContext.");
        }

        if (!licenseFeatureGate.HasFeature(TenantLicenseFeatures.Premium.MultiTenant))
        {
            throw new InvalidOperationException(
                "[LICENSE] Feature 'multi-tenant' is not available under the current license tier.");
        }
    }

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
