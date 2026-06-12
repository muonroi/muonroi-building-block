namespace Muonroi.Tenancy.Abstractions.Tests;

public class InMemoryTenantQuotaTrackerTests
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
    public async Task CheckQuota_Returns_True_When_Under_Limit()
    {
        InMemoryTenantQuotaStore store = new(new FakeDateTimeService(), new MJsonSerializeService());
        TenantQuota quota = TenantQuotaPresets.Free;
        quota.MaxApiRequestsPerMinute = 5;
        await store.SaveQuotaAsync("tenant-a", quota);
        InMemoryTenantQuotaTracker tracker = new(store);

        bool allowed = await tracker.CheckQuotaAsync("tenant-a", QuotaType.ApiRequestsPerMinute, 1);

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckQuota_Returns_False_When_Over_Limit()
    {
        InMemoryTenantQuotaStore store = new(new FakeDateTimeService(), new MJsonSerializeService());
        TenantQuota quota = TenantQuotaPresets.Free;
        quota.MaxApiRequestsPerMinute = 5;
        await store.SaveQuotaAsync("tenant-a", quota);
        InMemoryTenantQuotaTracker tracker = new(store);
        await tracker.IncrementUsageAsync("tenant-a", QuotaType.ApiRequestsPerMinute, 5);

        bool allowed = await tracker.CheckQuotaAsync("tenant-a", QuotaType.ApiRequestsPerMinute, 1);

        allowed.Should().BeFalse();
    }
}
