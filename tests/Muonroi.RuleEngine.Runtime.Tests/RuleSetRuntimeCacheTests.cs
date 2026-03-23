using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Muonroi.RuleEngine.Runtime.Rules;
using NSubstitute;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleSetRuntimeCacheTests : IDisposable
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly RuleStoreConfigs _configs = new() { EnableRuntimeCache = true, RuntimeCacheMinutes = 10 };

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetOrCreateAsync_WhenCacheDisabled_CallsFactoryDirectly()
    {
        RuleStoreConfigs configs = new() { EnableRuntimeCache = false };
        using RuleSetRuntimeCache sut = new(_cache, configs);
        int factoryCalled = 0;

        string? result = await sut.GetOrCreateAsync("t1", "wf1", () =>
        {
            factoryCalled++;
            return Task.FromResult<string?>("payload");
        });

        result.Should().Be("payload");
        factoryCalled.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenCacheEnabled_ReturnsCachedValue()
    {
        using RuleSetRuntimeCache sut = new(_cache, _configs);
        int factoryCalled = 0;

        await sut.GetOrCreateAsync("t1", "wf1", () =>
        {
            factoryCalled++;
            return Task.FromResult<string?>("payload");
        });

        string? result = await sut.GetOrCreateAsync("t1", "wf1", () =>
        {
            factoryCalled++;
            return Task.FromResult<string?>("new-payload");
        });

        result.Should().Be("payload");
        factoryCalled.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenFactoryReturnsNull_DoesNotCache()
    {
        using RuleSetRuntimeCache sut = new(_cache, _configs);
        int factoryCalled = 0;

        await sut.GetOrCreateAsync("t1", "wf1", () =>
        {
            factoryCalled++;
            return Task.FromResult<string?>(null);
        });

        await sut.GetOrCreateAsync("t1", "wf1", () =>
        {
            factoryCalled++;
            return Task.FromResult<string?>("payload");
        });

        factoryCalled.Should().Be(2);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesCachedEntry()
    {
        using RuleSetRuntimeCache sut = new(_cache, _configs);

        await sut.GetOrCreateAsync("t1", "wf1", () => Task.FromResult<string?>("payload"));
        await sut.InvalidateAsync("t1", "wf1");

        int factoryCalled = 0;
        string? result = await sut.GetOrCreateAsync("t1", "wf1", () =>
        {
            factoryCalled++;
            return Task.FromResult<string?>("new-payload");
        });

        result.Should().Be("new-payload");
        factoryCalled.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentTenantsAndWorkflows_AreCachedSeparately()
    {
        using RuleSetRuntimeCache sut = new(_cache, _configs);

        await sut.GetOrCreateAsync("t1", "wf1", () => Task.FromResult<string?>("t1-wf1"));
        await sut.GetOrCreateAsync("t2", "wf1", () => Task.FromResult<string?>("t2-wf1"));
        await sut.GetOrCreateAsync("t1", "wf2", () => Task.FromResult<string?>("t1-wf2"));

        string? r1 = await sut.GetOrCreateAsync("t1", "wf1", () => Task.FromResult<string?>("x"));
        string? r2 = await sut.GetOrCreateAsync("t2", "wf1", () => Task.FromResult<string?>("x"));
        string? r3 = await sut.GetOrCreateAsync("t1", "wf2", () => Task.FromResult<string?>("x"));

        r1.Should().Be("t1-wf1");
        r2.Should().Be("t2-wf1");
        r3.Should().Be("t1-wf2");
    }

    [Fact]
    public async Task GetOrCreateAsync_CancellationRequested_Throws()
    {
        using RuleSetRuntimeCache sut = new(_cache, _configs);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = () => sut.GetOrCreateAsync("t1", "wf1",
            () => Task.FromResult<string?>("x"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InvalidateAsync_CancellationRequested_Throws()
    {
        using RuleSetRuntimeCache sut = new(_cache, _configs);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = () => sut.InvalidateAsync("t1", "wf1", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_WithNotifier_SubscribesToChanges()
    {
        InMemoryRuleSetChangeNotifier notifier = new();
        using RuleSetRuntimeCache sut = new(_cache, _configs, notifier);

        // Should not throw - subscription created
        sut.Dispose();
    }

    [Fact]
    public async Task GetOrCreateAsync_CaseInsensitiveCacheKey()
    {
        using RuleSetRuntimeCache sut = new(_cache, _configs);

        await sut.GetOrCreateAsync("Tenant1", "Workflow1", () => Task.FromResult<string?>("cached"));
        string? result = await sut.GetOrCreateAsync("tenant1", "workflow1", () => Task.FromResult<string?>("miss"));

        result.Should().Be("cached");
    }

    [Fact]
    public async Task GetOrCreateAsync_DefaultRuntimeCacheMinutes_UsesDefault()
    {
        RuleStoreConfigs configs = new() { EnableRuntimeCache = true, RuntimeCacheMinutes = 0 };
        using RuleSetRuntimeCache sut = new(_cache, configs);

        await sut.GetOrCreateAsync("t1", "wf1", () => Task.FromResult<string?>("payload"));
        string? result = await sut.GetOrCreateAsync("t1", "wf1", () => Task.FromResult<string?>("miss"));

        result.Should().Be("payload");
    }
}
