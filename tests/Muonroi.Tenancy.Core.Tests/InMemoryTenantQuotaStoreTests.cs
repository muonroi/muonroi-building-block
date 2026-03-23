using FluentAssertions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Tenancy.Abstractions.Models;
using NSubstitute;
using Xunit;

namespace Muonroi.Tenancy.Core.Tests;

public class InMemoryTenantQuotaStoreTests
{
    private static Muonroi.Tenancy.Core.Shared.InMemoryTenantQuotaStore CreateStore()
    {
        IMDateTimeService dateTimeService = Substitute.For<IMDateTimeService>();
        dateTimeService.UtcNow().Returns(new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc));

        IMJsonSerializeService jsonService = Substitute.For<IMJsonSerializeService>();
        // Real clone behavior: serialize then deserialize returns a copy
        jsonService.Serialize(Arg.Any<TenantQuota>()).Returns(callInfo =>
        {
            TenantQuota q = callInfo.Arg<TenantQuota>();
            return System.Text.Json.JsonSerializer.Serialize(q);
        });
        jsonService.Deserialize<TenantQuota>(Arg.Any<string>()).Returns(callInfo =>
        {
            string json = callInfo.Arg<string>();
            return System.Text.Json.JsonSerializer.Deserialize<TenantQuota>(json);
        });

        return new Muonroi.Tenancy.Core.Shared.InMemoryTenantQuotaStore(dateTimeService, jsonService);
    }

    [Fact]
    public async Task GetQuotaAsync_Returns_Null_When_No_Quota_Saved()
    {
        Muonroi.Tenancy.Core.Shared.InMemoryTenantQuotaStore store = CreateStore();

        TenantQuota? result = await store.GetQuotaAsync("unknown-tenant");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveQuotaAsync_And_GetQuotaAsync_Roundtrip()
    {
        Muonroi.Tenancy.Core.Shared.InMemoryTenantQuotaStore store = CreateStore();
        TenantQuota quota = new() { MaxRuleExecutionsPerDay = 500 };

        await store.SaveQuotaAsync("tenant-1", quota);
        TenantQuota? result = await store.GetQuotaAsync("tenant-1");

        result.Should().NotBeNull();
        result!.MaxRuleExecutionsPerDay.Should().Be(500);
        result.TenantId.Should().Be("tenant-1");
    }

    [Fact]
    public async Task RecordUsageAsync_Accumulates_Usage()
    {
        Muonroi.Tenancy.Core.Shared.InMemoryTenantQuotaStore store = CreateStore();

        await store.RecordUsageAsync("tenant-1", QuotaType.RuleExecutionsPerDay, 5);
        await store.RecordUsageAsync("tenant-1", QuotaType.RuleExecutionsPerDay, 3);

        QuotaUsage usage = await store.GetUsageAsync("tenant-1");
        usage.CurrentUsage[QuotaType.RuleExecutionsPerDay].Should().Be(8);
    }

    [Fact]
    public async Task GetUsageAsync_Uses_Free_Tier_Limits_When_No_Quota_Configured()
    {
        Muonroi.Tenancy.Core.Shared.InMemoryTenantQuotaStore store = CreateStore();

        QuotaUsage usage = await store.GetUsageAsync("new-tenant");

        usage.TenantId.Should().Be("new-tenant");
        usage.Limits.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ResetDailyCountersAsync_Clears_All_Usage()
    {
        Muonroi.Tenancy.Core.Shared.InMemoryTenantQuotaStore store = CreateStore();
        await store.RecordUsageAsync("tenant-1", QuotaType.RuleExecutionsPerDay, 10);

        await store.ResetDailyCountersAsync();

        QuotaUsage usage = await store.GetUsageAsync("tenant-1");
        usage.CurrentUsage.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveQuotaAsync_Sets_TenantId_On_Quota()
    {
        Muonroi.Tenancy.Core.Shared.InMemoryTenantQuotaStore store = CreateStore();
        TenantQuota quota = new() { TenantId = "wrong-id" };

        await store.SaveQuotaAsync("correct-id", quota);
        TenantQuota? result = await store.GetQuotaAsync("correct-id");

        result!.TenantId.Should().Be("correct-id");
    }
}
