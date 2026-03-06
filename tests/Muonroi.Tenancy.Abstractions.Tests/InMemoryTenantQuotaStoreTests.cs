namespace Muonroi.Tenancy.Abstractions.Tests;

public class InMemoryTenantQuotaStoreTests
{
    private sealed class FakeDateTimeService : IMDateTimeService
    {
        private static readonly DateTime Utc = new(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc);

        public DateTime Now() => Utc.ToLocalTime();
        public DateTime UtcNow() => Utc;
        public DateTime Today() => Now().Date;
        public DateTime UtcToday() => Utc.Date;
        public double NowTs() => new DateTimeOffset(Now()).ToUnixTimeSeconds();
        public double UtcNowTs() => new DateTimeOffset(Utc).ToUnixTimeSeconds();
    }

    [Fact]
    public async Task SaveQuota_And_GetQuota_RoundTrip()
    {
        InMemoryTenantQuotaStore store = new(new FakeDateTimeService(), new MJsonSerializeService());
        TenantQuota quota = TenantQuotaPresets.Free;

        await store.SaveQuotaAsync("tenant-a", quota);
        TenantQuota? loaded = await store.GetQuotaAsync("tenant-a");

        loaded.Should().NotBeNull();
        loaded!.TenantId.Should().Be("tenant-a");
        loaded.MaxApiRequestsPerMinute.Should().Be(quota.MaxApiRequestsPerMinute);
    }

    [Fact]
    public async Task RecordUsage_Aggregates_By_QuotaType()
    {
        InMemoryTenantQuotaStore store = new(new FakeDateTimeService(), new MJsonSerializeService());

        await store.RecordUsageAsync("tenant-a", QuotaType.ApiRequestsPerMinute, 2);
        await store.RecordUsageAsync("tenant-a", QuotaType.ApiRequestsPerMinute, 3);
        QuotaUsage usage = await store.GetUsageAsync("tenant-a");

        usage.CurrentUsage[QuotaType.ApiRequestsPerMinute].Should().Be(5);
    }
}
