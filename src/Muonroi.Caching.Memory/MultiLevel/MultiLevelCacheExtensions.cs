namespace Muonroi.Caching.Memory.MultiLevel;

/// <summary>
/// Service registration helpers for multi-level caching.
/// </summary>
public static class MultiLevelCacheExtensions
{
    /// <summary>
    /// Registers memory and distributed caches plus the multi-level cache service.
    /// </summary>
    /// <param name="services">Service collection to update.</param>
    /// <param name="configuration">Configuration source.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMultiLevelCaching(this IServiceCollection services, IConfiguration configuration)
    {
        _ = services.AddMemoryCache();
        _ = services.AddDistributedMemoryCache();

        _ = services.AddSingleton<IMultiLevelCacheService, MultiLevelCacheService>();

        return services;
    }
}
