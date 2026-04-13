using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Muonroi.Tenancy;

namespace Muonroi.BuildingBlock.Test.Tenancy;

public sealed class TenantQuotaTests
{
    [Fact]
    public async Task CheckQuota_WhenUnderLimit_ReturnsTrue()
    {
        MemoryDistributedCache cache = new(Options.Create(new MemoryDistributedCacheOptions()));
        InMemoryTenantQuotaStore store = new();
        TenantQuota quota = TenantQuotaPresets.Free;
        quota.TenantId = "tenant1";
        await store.SaveQuotaAsync("tenant1", quota, default);

        ITenantQuotaTracker tracker = new TenantQuotaTracker(cache, store, NullLogger<TenantQuotaTracker>.Instance);
        bool allowed = await tracker.CheckQuotaAsync("tenant1", QuotaType.ApiRequestsPerMinute, 1, default);
        Assert.True(allowed);
    }

    [Fact]
    public async Task CheckQuota_WhenOverLimit_ReturnsFalse()
    {
        MemoryDistributedCache cache = new(Options.Create(new MemoryDistributedCacheOptions()));
        InMemoryTenantQuotaStore store = new();
        TenantQuota quota = TenantQuotaPresets.Free;
        quota.MaxApiRequestsPerMinute = 5;
        await store.SaveQuotaAsync("tenant1", quota, default);

        ITenantQuotaTracker tracker = new TenantQuotaTracker(cache, store, NullLogger<TenantQuotaTracker>.Instance);
        for (int i = 0; i < 5; i++)
        {
            await tracker.IncrementUsageAsync("tenant1", QuotaType.ApiRequestsPerMinute, 1, default);
        }

        bool allowed = await tracker.CheckQuotaAsync("tenant1", QuotaType.ApiRequestsPerMinute, 1, default);
        Assert.False(allowed);
    }

    [Fact]
    public async Task RuleOrchestrator_WhenQuotaExceeded_ThrowsException()
    {
        MemoryDistributedCache cache = new(Options.Create(new MemoryDistributedCacheOptions()));
        InMemoryTenantQuotaStore store = new();
        TenantQuota quota = TenantQuotaPresets.Free;
        quota.MaxConcurrentExecutions = 1;
        await store.SaveQuotaAsync("tenant1", quota, default);

        ITenantQuotaTracker tracker = new TenantQuotaTracker(cache, store, NullLogger<TenantQuotaTracker>.Instance);
        await tracker.IncrementUsageAsync("tenant1", QuotaType.ConcurrentExecutions, 1, default);

        RuleOrchestrator<TestContext> orchestrator = new(
            [],
            [],
            NullLogger<RuleOrchestrator<TestContext>>.Instance,
            null,
            tracker);

        await Assert.ThrowsAsync<QuotaExceededException>(() =>
        {
            TestContext context = new()
            {
                TenantId = "tenant1"
            };
            return orchestrator.ExecuteAsync(context, cancellationToken: default);
        });
    }

    private sealed class TestContext
    {
        public string TenantId { get; set; } = string.Empty;
    }
}
