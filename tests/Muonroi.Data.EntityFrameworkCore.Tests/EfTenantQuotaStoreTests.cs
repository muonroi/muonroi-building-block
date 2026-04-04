using Muonroi.Quota.Abstractions;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class EfTenantQuotaStoreTests
{
    private sealed class QuotaTestDbContext(DbContextOptions<QuotaTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    private static (QuotaTestDbContext db, EfTenantQuotaStore<QuotaTestDbContext> store) CreateSut(
        IMDateTimeService? dateTimeService = null)
    {
        TenantContext.CurrentTenantId = null;
        DbContextOptions<QuotaTestDbContext> options = new DbContextOptionsBuilder<QuotaTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        QuotaTestDbContext db = new(options);
        dateTimeService ??= new MDateTimeService();
        EfTenantQuotaStore<QuotaTestDbContext> store = new(db, dateTimeService);
        return (db, store);
    }

    [Fact]
    public async Task GetQuotaAsync_Returns_Null_When_Not_Found()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();

        TenantQuota? result = await store.GetQuotaAsync("tenant-missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveQuotaAsync_Creates_New_And_GetQuotaAsync_Returns_It()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();
        string tenantId = "tenant-save-1";
        TenantQuota quota = new()
        {
            Tier = TenantTier.Enterprise,
            MaxRulesPerTenant = 100,
            MaxRuleExecutionsPerDay = 1000,
            MaxConcurrentExecutions = 10,
            MaxDecisionTables = 50,
            MaxJsonWorkflows = 20,
            MaxStorageMB = 500,
            MaxApiRequestsPerMinute = 60,
            MaxRuleEvaluationsPerSecond = 100,
            MaxWorkflowExecutionsPerHour = 50,
            MaxRuleComplexity = 10,
            MaxWorkflowSizeKB = 1024,
            MaxExecutionTimeMs = 5000,
            MaxTotalConnectors = 10,
            MaxConnectorExecutionsPerDay = 100
        };

        await store.SaveQuotaAsync(tenantId, quota);
        TenantQuota? result = await store.GetQuotaAsync(tenantId);

        result.Should().NotBeNull();
        result!.Tier.Should().Be(TenantTier.Enterprise);
        result.MaxRulesPerTenant.Should().Be(100);
        result.MaxConcurrentExecutions.Should().Be(10);
    }

    [Fact]
    public async Task SaveQuotaAsync_Updates_Existing()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();
        string tenantId = "tenant-update";
        TenantQuota initial = new() { Tier = TenantTier.Free, MaxRulesPerTenant = 5 };
        await store.SaveQuotaAsync(tenantId, initial);

        TenantQuota updated = new() { Tier = TenantTier.Professional, MaxRulesPerTenant = 50 };
        await store.SaveQuotaAsync(tenantId, updated);

        TenantQuota? result = await store.GetQuotaAsync(tenantId);
        result.Should().NotBeNull();
        result!.Tier.Should().Be(TenantTier.Professional);
        result.MaxRulesPerTenant.Should().Be(50);
    }

    [Fact]
    public async Task SaveQuotaAsync_Throws_On_Null_TenantId()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();

        Func<Task> act = () => store.SaveQuotaAsync(null!, new TenantQuota());

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task SaveQuotaAsync_Throws_On_Null_Quota()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();

        Func<Task> act = () => store.SaveQuotaAsync("tenant", null!);

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task RecordUsageAsync_Creates_New_Usage_Row()
    {
        (QuotaTestDbContext db, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();
        string tenantId = "tenant-usage";

        await store.RecordUsageAsync(tenantId, QuotaType.RuleExecutionsPerDay, 5);

        int count = await db.TenantQuotaUsages.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task RecordUsageAsync_Increments_Existing_Usage()
    {
        (QuotaTestDbContext db, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();
        string tenantId = "tenant-incr";

        await store.RecordUsageAsync(tenantId, QuotaType.RuleExecutionsPerDay, 5);
        await store.RecordUsageAsync(tenantId, QuotaType.RuleExecutionsPerDay, 3);

        MTenantQuotaUsage usage = await db.TenantQuotaUsages.FirstAsync();
        usage.Amount.Should().Be(8);
    }

    [Fact]
    public async Task RecordUsageAsync_Zero_Amount_Does_Nothing()
    {
        (QuotaTestDbContext db, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();

        await store.RecordUsageAsync("tenant-zero", QuotaType.RuleExecutionsPerDay, 0);

        int count = await db.TenantQuotaUsages.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task RecordUsageAsync_Throws_On_Null_TenantId()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();

        Func<Task> act = () => store.RecordUsageAsync(null!, QuotaType.RuleExecutionsPerDay, 1);

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task GetUsageAsync_Returns_Usage_Snapshot()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();
        string tenantId = "tenant-getusage";
        await store.SaveQuotaAsync(tenantId, new TenantQuota { MaxRulesPerTenant = 50, Tier = TenantTier.Free });
        await store.RecordUsageAsync(tenantId, QuotaType.RuleExecutionsPerDay, 10);

        QuotaUsage result = await store.GetUsageAsync(tenantId);

        result.TenantId.Should().Be(tenantId);
        result.CurrentUsage.Should().ContainKey(QuotaType.RuleExecutionsPerDay);
        result.Limits.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUsageAsync_Returns_Defaults_When_No_Quota()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();

        QuotaUsage result = await store.GetUsageAsync("tenant-noq");

        result.TenantId.Should().Be("tenant-noq");
        result.Limits.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetQuotaAsync_Throws_On_Empty_TenantId()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();

        Func<Task> act = () => store.GetQuotaAsync("");

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task GetUsageAsync_Throws_On_Empty_TenantId()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();

        Func<Task> act = () => store.GetUsageAsync("");

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task ResetDailyCountersAsync_Does_Not_Remove_Current_Rows()
    {
        // ResetDailyCountersAsync only removes stale rows (PeriodEnd < today).
        // Recording a current-day usage should NOT be removed.
        (QuotaTestDbContext db, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();
        await store.RecordUsageAsync("tenant-current2", QuotaType.RuleExecutionsPerDay, 3);

        await store.ResetDailyCountersAsync();

        int count = await db.TenantQuotaUsages.IgnoreQueryFilters().CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task ResetDailyCountersAsync_Keeps_Current_Rows()
    {
        (_, EfTenantQuotaStore<QuotaTestDbContext> store) = CreateSut();
        await store.RecordUsageAsync("tenant-current", QuotaType.RuleExecutionsPerDay, 1);

        await store.ResetDailyCountersAsync();

        QuotaUsage usage = await store.GetUsageAsync("tenant-current");
        usage.CurrentUsage[QuotaType.RuleExecutionsPerDay].Should().Be(1);
    }
}
