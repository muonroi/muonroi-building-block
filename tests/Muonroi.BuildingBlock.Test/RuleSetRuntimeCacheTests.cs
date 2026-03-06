namespace Muonroi.BuildingBlock.Test;

public class RuleSetRuntimeCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_ShouldCacheUntilInvalidatedByNotifier()
    {
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        InMemoryRuleSetChangeNotifier notifier = new();
        RuleStoreConfigs configs = new()
        {
            EnableRuntimeCache = true,
            RuntimeCacheMinutes = 60
        };
        RuleSetRuntimeCache cache = new(memory, configs, notifier);

        int callCount = 0;
        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>("payload");
        }

        string? first = await cache.GetOrCreateAsync("tenant-a", "wf", Factory);
        string? second = await cache.GetOrCreateAsync("tenant-a", "wf", Factory);
        Assert.Equal("payload", first);
        Assert.Equal("payload", second);
        Assert.Equal(1, callCount);

        await notifier.PublishAsync(new RuleSetChangeEvent(
            "tenant-a",
            "wf",
            RuleSetChangeTypes.Saved,
            null,
            DateTimeOffset.UtcNow));

        _ = await cache.GetOrCreateAsync("tenant-a", "wf", Factory);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenCacheDisabled_ShouldAlwaysCallFactory()
    {
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        RuleStoreConfigs configs = new()
        {
            EnableRuntimeCache = false
        };
        RuleSetRuntimeCache cache = new(memory, configs);

        int callCount = 0;
        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>("payload");
        }

        _ = await cache.GetOrCreateAsync("tenant-a", "wf", Factory);
        _ = await cache.GetOrCreateAsync("tenant-a", "wf", Factory);

        Assert.Equal(2, callCount);
    }
}
