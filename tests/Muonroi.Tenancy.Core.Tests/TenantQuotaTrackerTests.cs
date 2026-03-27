using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Logging.Abstractions;
using Muonroi.Quota.Abstractions;
using Muonroi.Tenancy.Core.Shared;
using NSubstitute;
using Xunit;

namespace Muonroi.Tenancy.Core.Tests;

public class TenantQuotaTrackerTests
{
    private sealed class TrackerFixture
    {
        public IDistributedCache Cache { get; }
        public ITenantQuotaStore QuotaStore { get; }
        public IMLog<TenantQuotaTracker> Logger { get; }
        public IMDateTimeService DateTimeService { get; }
        public TenantQuotaTracker Tracker { get; }

        public TrackerFixture()
        {
            Cache = Substitute.For<IDistributedCache>();
            QuotaStore = Substitute.For<ITenantQuotaStore>();
            Logger = Substitute.For<IMLog<TenantQuotaTracker>>();
            DateTimeService = Substitute.For<IMDateTimeService>();
            DateTimeService.UtcNow().Returns(new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc));

            Tracker = new TenantQuotaTracker(Cache, QuotaStore, Logger, DateTimeService);
        }

        public void SetupQuota(string tenantId, TenantQuota quota)
        {
            QuotaStore.GetQuotaAsync(tenantId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<TenantQuota?>(quota));
        }

        public void SetupCacheValue(string tenantId, QuotaType type, string value)
        {
            // Build key matching the tracker's internal format
            string periodKey = type switch
            {
                QuotaType.RuleEvaluationsPerSecond => "20260321120000",
                QuotaType.ApiRequestsPerMinute => "202603211200",
                QuotaType.WorkflowExecutionsPerHour => "2026032112",
                _ => "20260321"
            };
            string key = $"quota:{tenantId}:{type}:{periodKey}";
            Cache.GetAsync(key, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<byte[]?>(System.Text.Encoding.UTF8.GetBytes(value)));
        }
    }

    [Fact]
    public async Task CheckQuotaAsync_Returns_True_When_Under_Limit()
    {
        TrackerFixture fixture = new();
        fixture.SetupQuota("tenant-1", new TenantQuota { MaxRuleExecutionsPerDay = 100 });
        fixture.SetupCacheValue("tenant-1", QuotaType.RuleExecutionsPerDay, "50");

        bool result = await fixture.Tracker.CheckQuotaAsync("tenant-1", QuotaType.RuleExecutionsPerDay);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckQuotaAsync_Returns_False_When_At_Limit()
    {
        TrackerFixture fixture = new();
        fixture.SetupQuota("tenant-1", new TenantQuota { MaxRuleExecutionsPerDay = 100 });
        fixture.SetupCacheValue("tenant-1", QuotaType.RuleExecutionsPerDay, "100");

        bool result = await fixture.Tracker.CheckQuotaAsync("tenant-1", QuotaType.RuleExecutionsPerDay);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckQuotaAsync_Returns_True_When_Limit_Is_MaxValue()
    {
        TrackerFixture fixture = new();
        fixture.SetupQuota("tenant-1", new TenantQuota { MaxRuleExecutionsPerDay = int.MaxValue });

        bool result = await fixture.Tracker.CheckQuotaAsync("tenant-1", QuotaType.RuleExecutionsPerDay);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckQuotaAsync_Defaults_To_Free_Tier_When_No_Quota()
    {
        TrackerFixture fixture = new();
        fixture.SetupCacheValue("unknown", QuotaType.RuleExecutionsPerDay, "1000");

        bool result = await fixture.Tracker.CheckQuotaAsync("unknown", QuotaType.RuleExecutionsPerDay);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IncrementUsageAsync_Writes_To_Cache_And_Records_Usage()
    {
        TrackerFixture fixture = new();

        await fixture.Tracker.IncrementUsageAsync("tenant-1", QuotaType.RuleExecutionsPerDay, 5);

        await fixture.Cache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
        await fixture.QuotaStore.Received(1).RecordUsageAsync("tenant-1", QuotaType.RuleExecutionsPerDay, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsageAsync_Delegates_To_QuotaStore()
    {
        TrackerFixture fixture = new();
        QuotaUsage expected = new() { TenantId = "tenant-1" };
        fixture.QuotaStore.GetUsageAsync("tenant-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        QuotaUsage result = await fixture.Tracker.GetUsageAsync("tenant-1");

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ResetDailyQuotasAsync_Delegates_To_QuotaStore()
    {
        TrackerFixture fixture = new();

        await fixture.Tracker.ResetDailyQuotasAsync();

        await fixture.QuotaStore.Received(1).ResetDailyCountersAsync(Arg.Any<CancellationToken>());
    }
}
