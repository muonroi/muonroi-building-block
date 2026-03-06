namespace Muonroi.Tenancy.Abstractions.Tests;

public class TenantQuotaServiceCollectionExtensionsTests
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
    public void AddTenantQuotaManagement_Registers_Default_Services()
    {
        ServiceCollection services = [];
        services.AddSingleton<IMDateTimeService, FakeDateTimeService>();
        services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();

        services.AddTenantQuotaManagement();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITenantQuotaStore>().Should().BeOfType<InMemoryTenantQuotaStore>();
        provider.GetRequiredService<ITenantQuotaTracker>().Should().BeOfType<InMemoryTenantQuotaTracker>();
    }
}
