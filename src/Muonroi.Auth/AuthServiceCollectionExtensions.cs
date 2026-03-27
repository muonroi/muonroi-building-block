using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Auth;

/// <summary>
/// Extension methods for registering authentication services in the dependency injection container.
/// </summary>
public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers an in-memory RSA key store and token revocation store.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddInMemoryRsaKeyStore(this IServiceCollection services)
    {
        services.RemoveAll<IRsaKeyStore>();
        services.RemoveAll<ITokenRevocationStore>();

        services.TryAddSingleton<IRsaKeyStore, InMemoryRsaKeyStore>();
        services.TryAddSingleton<ITokenRevocationStore, TokenRevocationStore>();
        RegisterJwtService(services);
        return services;
    }

    /// <summary>
    /// Registers a Redis-backed RSA key store and token revocation store.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration to bind settings from.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddRedisRsaKeyStore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.RemoveAll<IRsaKeyStore>();
        services.RemoveAll<ITokenRevocationStore>();

        services.AddSingleton<IRsaKeyStore>(sp =>
            new RedisRsaKeyStore(
                sp.GetRequiredService<IDistributedCache>(),
                configuration,
                sp.GetRequiredService<IMJsonSerializeService>()));
        services.TryAddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();
        RegisterJwtService(services);
        return services;
    }

    private static void RegisterJwtService(IServiceCollection services)
    {
        services.RemoveAll<JwtService>();
        services.AddSingleton(sp =>
        {
            MTokenInfo tokenInfo = ResolveTokenInfo(sp);
            return new JwtService(
                sp.GetRequiredService<IRsaKeyStore>(),
                sp.GetRequiredService<ITokenRevocationStore>(),
                tokenInfo,
                sp.GetRequiredService<IMDateTimeService>());
        });
    }

    private static MTokenInfo ResolveTokenInfo(IServiceProvider sp)
    {
        MTokenInfo? existing = sp.GetService<MTokenInfo>();
        if (existing is not null)
        {
            return existing;
        }

        IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
        MTokenInfo tokenInfo = new();
        configuration.GetSection(tokenInfo.SectionName).Bind(tokenInfo);
        return tokenInfo;
    }
}
