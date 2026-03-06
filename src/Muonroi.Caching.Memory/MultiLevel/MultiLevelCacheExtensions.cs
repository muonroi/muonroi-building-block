namespace Muonroi.Caching.Memory.MultiLevel;

public static class MultiLevelCacheExtensions
{
    public static IServiceCollection AddMultiLevelCaching(this IServiceCollection services, IConfiguration configuration)
    {
        _ = services.AddMemoryCache();
        _ = services.AddDistributedMemoryCache();

        _ = services.AddSingleton<IMultiLevelCacheService, MultiLevelCacheService>();

        return services;
    }
}
