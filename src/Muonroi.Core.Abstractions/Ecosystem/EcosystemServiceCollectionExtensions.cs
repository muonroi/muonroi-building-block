using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Muonroi.Core.Abstractions.Ecosystem;

/// <summary>
/// DI helpers for the ecosystem capability registry.
/// </summary>
public static class EcosystemServiceCollectionExtensions
{
    /// <summary>
    /// Ensures an <see cref="IMEcosystemRegistry"/> singleton is registered and returns the
    /// instance that will be used at runtime.
    /// Uses <c>TryAddSingleton</c> so the first registration wins — subsequent calls from
    /// other packages are no-ops.
    /// Also automatically registers <see cref="MEcosystemStartupFilter"/> for startup capability logging.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The <see cref="IMEcosystemRegistry"/> singleton resolved from a temporary provider.</returns>
    public static IMEcosystemRegistry GetOrCreateRegistry(this IServiceCollection services)
    {
        // Check if IMEcosystemRegistry is already registered.
        // If so, we cannot retrieve the existing instance without building a provider,
        // but we need to return the same instance that will be used at runtime.
        // Strategy: register as instance singleton — the exact instance we create here
        // is what gets stored in the DI container, so callers registering capabilities
        // on it are writing to the same object the app will resolve at runtime.
        ServiceDescriptor? existing = services
            .FirstOrDefault(sd => sd.ServiceType == typeof(IMEcosystemRegistry));

        if (existing is null)
        {
            MEcosystemRegistry registry = new();
            services.AddSingleton<IMEcosystemRegistry>(registry);
            services.AddEcosystemStartupLog();
            return registry;
        }

        // Already registered — resolve from a temporary provider to get the existing instance.
        using var sp = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false });

        return sp.GetRequiredService<IMEcosystemRegistry>();
    }

    /// <summary>
    /// Registers <see cref="MEcosystemStartupFilter"/> as an <see cref="IStartupFilter"/> singleton.
    /// Idempotent — uses <c>TryAddEnumerable</c> so multiple calls are safe.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddEcosystemStartupLog(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, MEcosystemStartupFilter>());
        return services;
    }
}
