namespace Muonroi.BuildingBlock.Test;

public class MultiLevelCacheExtensionsTests
{
    [Fact]
    public void AddMultiLevelCaching_Behaviors()
    {
        Dictionary<string, string?> data = new()
        {
            ["CacheConfigs:CacheType"] = "MultiLevel",
            ["RedisConfigs:Host"] = "localhost",
            ["RedisConfigs:Port"] = "6379",
            ["RedisConfigs:Password"] = "pwd",
            ["RedisConfigs:KeyPrefix"] = "pre"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        services.AddDistributedMemoryCache();
        services.AddMultiLevelCaching(config);
        services.AddMultiLevelCaching(config);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IMultiLevelCacheService>());
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(CacheConfigs)));
        Assert.Throws<NullReferenceException>(() => services.AddMultiLevelCaching(null!));
    }

    [Fact]
    public void AddMultiLevelCaching_MultiLevelWithoutRedis_AddsDistributedMemoryFallback()
    {
        Dictionary<string, string?> data = new()
        {
            ["CacheConfigs:CacheType"] = "MultiLevel",
            ["RedisConfigs:Enable"] = "false"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];

        services.AddMultiLevelCaching(config);
        ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IMultiLevelCacheService>());
        Assert.NotNull(provider.GetService<IDistributedCache>());
    }
}
