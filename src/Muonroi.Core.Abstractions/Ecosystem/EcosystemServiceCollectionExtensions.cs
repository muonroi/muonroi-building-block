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
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The <see cref="IMEcosystemRegistry"/> singleton resolved from a temporary provider.</returns>
    public static IMEcosystemRegistry GetOrCreateRegistry(this IServiceCollection services)
    {
        services.TryAddSingleton<IMEcosystemRegistry, MEcosystemRegistry>();

        using var sp = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false });

        return sp.GetRequiredService<IMEcosystemRegistry>();
    }
}
