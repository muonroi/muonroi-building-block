namespace Muonroi.Billing.Abstractions.Tests;

/// <summary>
/// Tests for <see cref="UsageAggregator"/> (MON-03): deterministic pricing of a single
/// tenant's metered usage into <see cref="UsageLineItem"/>s via a <see cref="PricingPlan"/>.
/// </summary>
public sealed class UsageAggregatorTests
{
    private const string TenantId = "tenant-a";

    /// <summary>
    /// A stub <see cref="ITenantQuotaStore"/> that returns a fixed <see cref="QuotaUsage"/> and
    /// records the tenant ids it was queried with (to prove single-tenant reads, T-17-03).
    /// </summary>
    private sealed class StubQuotaStore : ITenantQuotaStore
    {
        private readonly QuotaUsage _usage;
        public List<string> QueriedTenants { get; } = [];

        public StubQuotaStore(QuotaUsage usage) => _usage = usage;

        public Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default)
        {
            QueriedTenants.Add(tenantId);
            return Task.FromResult(_usage);
        }

        // Unused members for these tests.
        public Task<TenantQuota?> GetQuotaAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult<TenantQuota?>(null);
        public Task SaveQuotaAsync(string tenantId, TenantQuota quota, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task RecordUsageAsync(string tenantId, QuotaType type, int amount, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task ResetDailyCountersAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static QuotaUsage UsageWith(Dictionary<QuotaType, int> currentUsage)
        => new()
        {
            TenantId = TenantId,
            CurrentUsage = currentUsage,
            PeriodStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        };

    [Fact]
    public async Task AggregateAsync_prices_metered_usage_plus_flat_base()
    {
        StubQuotaStore store = new(UsageWith(new Dictionary<QuotaType, int>
        {
            [QuotaType.PdfRendersPerDay] = 100,
            [QuotaType.ApiRequestsPerMinute] = 50,
        }));
        PricingPlan plan = new(
            TenantTier.Starter,
            new Dictionary<QuotaType, decimal> { [QuotaType.PdfRendersPerDay] = 0.02m },
            flatBaseAmount: 5m);
        UsageAggregator sut = new(store);

        IReadOnlyList<UsageLineItem> items = await sut.AggregateAsync(
            TenantId, plan, new DateTime(2026, 6, 1), new DateTime(2026, 7, 1));

        // PdfRendersPerDay: 100 * 0.02 = 2.00
        UsageLineItem pdf = items.Single(i => i.Dimension == QuotaType.PdfRendersPerDay && i.Description != UsageLineItem.FlatBaseDescription);
        pdf.Quantity.Should().Be(100);
        pdf.UnitRate.Should().Be(0.02m);
        pdf.Amount.Should().Be(2.00m);

        // ApiRequestsPerMinute: rate 0 -> 0-amount line item (documented deterministic choice: emit with 0 amount).
        UsageLineItem api = items.Single(i => i.Dimension == QuotaType.ApiRequestsPerMinute);
        api.Quantity.Should().Be(50);
        api.UnitRate.Should().Be(0m);
        api.Amount.Should().Be(0m);

        // Flat base appended LAST.
        UsageLineItem flat = items[^1];
        flat.Description.Should().Be(UsageLineItem.FlatBaseDescription);
        flat.Amount.Should().Be(5m);

        items.Sum(i => i.Amount).Should().Be(7.00m);
    }

    [Fact]
    public async Task AggregateAsync_is_deterministic_across_repeated_calls()
    {
        StubQuotaStore store = new(UsageWith(new Dictionary<QuotaType, int>
        {
            [QuotaType.PdfRendersPerDay] = 12,
            [QuotaType.MessagesPerDay] = 7,
            [QuotaType.ApiRequestsPerMinute] = 3,
        }));
        PricingPlan plan = new(
            TenantTier.Professional,
            new Dictionary<QuotaType, decimal>
            {
                [QuotaType.PdfRendersPerDay] = 0.05m,
                [QuotaType.MessagesPerDay] = 0.01m,
            },
            flatBaseAmount: 10m);
        UsageAggregator sut = new(store);

        IReadOnlyList<UsageLineItem> first = await sut.AggregateAsync(
            TenantId, plan, new DateTime(2026, 6, 1), new DateTime(2026, 7, 1));
        IReadOnlyList<UsageLineItem> second = await sut.AggregateAsync(
            TenantId, plan, new DateTime(2026, 6, 1), new DateTime(2026, 7, 1));

        second.Should().Equal(first);
    }

    [Fact]
    public async Task AggregateAsync_empty_usage_returns_only_flat_base()
    {
        StubQuotaStore store = new(UsageWith([]));
        PricingPlan plan = new(TenantTier.Free, flatBaseAmount: 3m);
        UsageAggregator sut = new(store);

        IReadOnlyList<UsageLineItem> items = await sut.AggregateAsync(
            TenantId, plan, new DateTime(2026, 6, 1), new DateTime(2026, 7, 1));

        items.Should().ContainSingle();
        items[0].Description.Should().Be(UsageLineItem.FlatBaseDescription);
        items[0].Amount.Should().Be(3m);
    }

    [Fact]
    public async Task AggregateAsync_empty_usage_and_zero_flat_base_returns_empty_list()
    {
        StubQuotaStore store = new(UsageWith([]));
        PricingPlan plan = new(TenantTier.Free, flatBaseAmount: 0m);
        UsageAggregator sut = new(store);

        IReadOnlyList<UsageLineItem> items = await sut.AggregateAsync(
            TenantId, plan, new DateTime(2026, 6, 1), new DateTime(2026, 7, 1));

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task AggregateAsync_reads_only_the_supplied_tenant()
    {
        StubQuotaStore store = new(UsageWith(new Dictionary<QuotaType, int>
        {
            [QuotaType.PdfRendersPerDay] = 1,
        }));
        PricingPlan plan = new(TenantTier.Starter, flatBaseAmount: 0m);
        UsageAggregator sut = new(store);

        await sut.AggregateAsync(TenantId, plan, new DateTime(2026, 6, 1), new DateTime(2026, 7, 1));

        store.QueriedTenants.Should().Equal(TenantId);
    }
}
