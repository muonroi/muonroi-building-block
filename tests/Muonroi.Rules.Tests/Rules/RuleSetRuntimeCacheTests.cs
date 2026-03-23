namespace Muonroi.Rules.Tests.Rules;

public class RuleSetRuntimeCacheTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    public void Dispose() => _memoryCache.Dispose();

    [Fact]
    public async Task GetOrCreateAsync_CacheEnabled_ShouldCacheResult()
    {
        var configs = new RuleStoreConfigs { EnableRuntimeCache = true, RuntimeCacheMinutes = 10 };
        var cache = new RuleSetRuntimeCache(_memoryCache, configs);

        int factoryCallCount = 0;
        Task<string?> Factory()
        {
            factoryCallCount++;
            return Task.FromResult<string?>("{\"test\":true}");
        }

        string? result1 = await cache.GetOrCreateAsync("t1", "wf1", Factory);
        string? result2 = await cache.GetOrCreateAsync("t1", "wf1", Factory);

        result1.Should().Be("{\"test\":true}");
        result2.Should().Be("{\"test\":true}");
        factoryCallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheDisabled_ShouldAlwaysCallFactory()
    {
        var configs = new RuleStoreConfigs { EnableRuntimeCache = false };
        var cache = new RuleSetRuntimeCache(_memoryCache, configs);

        int factoryCallCount = 0;
        Task<string?> Factory()
        {
            factoryCallCount++;
            return Task.FromResult<string?>("data");
        }

        await cache.GetOrCreateAsync("t1", "wf1", Factory);
        await cache.GetOrCreateAsync("t1", "wf1", Factory);

        factoryCallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetOrCreateAsync_NullFactory_ShouldNotCache()
    {
        var configs = new RuleStoreConfigs { EnableRuntimeCache = true };
        var cache = new RuleSetRuntimeCache(_memoryCache, configs);

        int factoryCallCount = 0;
        Task<string?> Factory()
        {
            factoryCallCount++;
            return Task.FromResult<string?>(null);
        }

        await cache.GetOrCreateAsync("t1", "wf1", Factory);
        await cache.GetOrCreateAsync("t1", "wf1", Factory);

        factoryCallCount.Should().Be(2);
    }

    [Fact]
    public async Task InvalidateAsync_ShouldRemoveCachedEntry()
    {
        var configs = new RuleStoreConfigs { EnableRuntimeCache = true };
        var cache = new RuleSetRuntimeCache(_memoryCache, configs);

        int factoryCallCount = 0;
        await cache.GetOrCreateAsync("t1", "wf1", () =>
        {
            factoryCallCount++;
            return Task.FromResult<string?>("data");
        });

        await cache.InvalidateAsync("t1", "wf1");

        await cache.GetOrCreateAsync("t1", "wf1", () =>
        {
            factoryCallCount++;
            return Task.FromResult<string?>("data2");
        });

        factoryCallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentTenants_ShouldCacheSeparately()
    {
        var configs = new RuleStoreConfigs { EnableRuntimeCache = true };
        var cache = new RuleSetRuntimeCache(_memoryCache, configs);

        var r1 = await cache.GetOrCreateAsync("t1", "wf1", () => Task.FromResult<string?>("tenant1-data"));
        var r2 = await cache.GetOrCreateAsync("t2", "wf1", () => Task.FromResult<string?>("tenant2-data"));

        r1.Should().Be("tenant1-data");
        r2.Should().Be("tenant2-data");
    }

    [Fact]
    public async Task InvalidateAsync_WithCancellation_ShouldThrow()
    {
        var configs = new RuleStoreConfigs { EnableRuntimeCache = true };
        var cache = new RuleSetRuntimeCache(_memoryCache, configs);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => cache.InvalidateAsync("t1", "wf1", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Dispose_WithNotifierSubscription_ShouldNotThrow()
    {
        var configs = new RuleStoreConfigs { EnableRuntimeCache = true };
        var notifier = new InMemoryRuleSetChangeNotifier();
        var cache = new RuleSetRuntimeCache(_memoryCache, configs, notifier);

        Action act = () => cache.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Notifier_ShouldInvalidateOnEvent()
    {
        var configs = new RuleStoreConfigs { EnableRuntimeCache = true };
        var notifier = new InMemoryRuleSetChangeNotifier();
        using var cache = new RuleSetRuntimeCache(_memoryCache, configs, notifier);

        int callCount = 0;
        await cache.GetOrCreateAsync("t1", "wf1", () =>
        {
            callCount++;
            return Task.FromResult<string?>("v1");
        });

        // Publish change event which should invalidate cache
        await notifier.PublishAsync(new RuleSetChangeEvent("t1", "wf1", "saved", 2, DateTimeOffset.UtcNow));

        await cache.GetOrCreateAsync("t1", "wf1", () =>
        {
            callCount++;
            return Task.FromResult<string?>("v2");
        });

        callCount.Should().Be(2);
    }
}
