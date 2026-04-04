using Muonroi.Quota.Abstractions;
using Muonroi.Tenancy.SiteProfile.Web.Behaviors;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// Integration tests for SiteQuotaBehavior (BTEL-03).
/// Verifies that ISiteQuotaEnforcer allows calls within quota limits,
/// throws SiteQuotaExceededException on N+1, and enforces quota per-site (not shared).
/// </summary>
public sealed class SiteQuotaEnforcementTests
{
    // -------------------------------------------------------------------------
    // Fake ITenantQuotaTracker — avoids deep DI chain (IMDateTimeService etc.)
    // -------------------------------------------------------------------------

    /// <summary>
    /// In-memory quota tracker with configurable per-(tenantId, QuotaType) limits.
    /// CheckQuotaAsync returns true when usage + amount &lt;= limit.
    /// IncrementUsageAsync adds to usage counter.
    /// </summary>
    private sealed class FakeTenantQuotaTracker(Dictionary<(string, QuotaType), int> limits) : ITenantQuotaTracker
    {
        // Key: (tenantId, quotaType) -> configured limit
        private readonly Dictionary<(string, QuotaType), int> _limits = limits;

        // Key: (tenantId, quotaType) -> current usage
        private readonly Dictionary<(string, QuotaType), int> _usage = new();

        public Task<bool> CheckQuotaAsync(
            string tenantId,
            QuotaType type,
            int amount = 1,
            CancellationToken ct = default)
        {
            var key = (tenantId, type);
            int current = _usage.TryGetValue(key, out var u) ? u : 0;

            if (!_limits.TryGetValue(key, out int limit))
            {
                // No limit configured — allow by default.
                return Task.FromResult(true);
            }

            return Task.FromResult(current + amount <= limit);
        }

        public Task IncrementUsageAsync(
            string tenantId,
            QuotaType type,
            int amount = 1,
            CancellationToken ct = default)
        {
            var key = (tenantId, type);
            _usage[key] = (_usage.TryGetValue(key, out var u) ? u : 0) + amount;
            return Task.CompletedTask;
        }

        public Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult(new QuotaUsage());

        public Task ResetDailyQuotasAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // BTEL-03 — SiteQuotaBehavior enforcement tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// BTEL-03: ISiteQuotaEnforcer allows calls up to the configured quota limit
    /// without throwing any exception.
    /// </summary>
    [Fact]
    public async Task SiteQuotaEnforcer_AllowsCallsWithinLimit()
    {
        // Arrange — limit of 2 for ALPHA
        var tracker = new FakeTenantQuotaTracker(new Dictionary<(string, QuotaType), int>
        {
            { ("ALPHA", QuotaType.ApiRequestsPerMinute), 2 }
        });

        var services = new ServiceCollection();
        services.AddSingleton<ITenantQuotaTracker>(tracker);

        var config = new ConfigurationBuilder().Build();
        new SiteQuotaBehavior().Apply(services, config, "ALPHA");

        await using var sp = services.BuildServiceProvider();

        // ISiteQuotaEnforcer is registered as Scoped — must resolve inside a scope.
        await using var scope = sp.CreateAsyncScope();
        var enforcer = scope.ServiceProvider.GetRequiredKeyedService<ISiteQuotaEnforcer>("ALPHA");

        // Act + Assert — first 2 calls must succeed
        var ex1 = await Record.ExceptionAsync(() =>
            enforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute));
        Assert.Null(ex1);

        var ex2 = await Record.ExceptionAsync(() =>
            enforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute));
        Assert.Null(ex2);
    }

    /// <summary>
    /// BTEL-03: ISiteQuotaEnforcer throws SiteQuotaExceededException on the (limit+1) call
    /// with the correct SiteId, QuotaType, and RequestedAmount properties.
    /// </summary>
    [Fact]
    public async Task SiteQuotaEnforcer_ThrowsSiteQuotaExceededException_OnExceedingLimit()
    {
        // Arrange — limit of 2 for ALPHA
        var tracker = new FakeTenantQuotaTracker(new Dictionary<(string, QuotaType), int>
        {
            { ("ALPHA", QuotaType.ApiRequestsPerMinute), 2 }
        });

        var services = new ServiceCollection();
        services.AddSingleton<ITenantQuotaTracker>(tracker);

        var config = new ConfigurationBuilder().Build();
        new SiteQuotaBehavior().Apply(services, config, "ALPHA");

        await using var sp = services.BuildServiceProvider();

        await using var scope = sp.CreateAsyncScope();
        var enforcer = scope.ServiceProvider.GetRequiredKeyedService<ISiteQuotaEnforcer>("ALPHA");

        // Exhaust the quota (2 calls)
        await enforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute);
        await enforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute);

        // Act — 3rd call must throw
        var ex = await Assert.ThrowsAsync<SiteQuotaExceededException>(
            () => enforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute));

        // Assert — exception carries correct context
        Assert.Equal("ALPHA", ex.SiteId);
        Assert.Equal(QuotaType.ApiRequestsPerMinute, ex.QuotaType);
        Assert.Equal(1, ex.RequestedAmount);
    }

    /// <summary>
    /// BTEL-03: Quota is enforced per-site — exhausting ALPHA's quota does not
    /// affect BRAVO's quota (isolation guarantee).
    /// </summary>
    [Fact]
    public async Task SiteQuotaEnforcer_QuotaIsPerSite_NotShared()
    {
        // Arrange — limit of 2 for both ALPHA and BRAVO
        var tracker = new FakeTenantQuotaTracker(new Dictionary<(string, QuotaType), int>
        {
            { ("ALPHA", QuotaType.ApiRequestsPerMinute), 2 },
            { ("BRAVO", QuotaType.ApiRequestsPerMinute), 2 }
        });

        var services = new ServiceCollection();
        services.AddSingleton<ITenantQuotaTracker>(tracker);

        var config = new ConfigurationBuilder().Build();
        new SiteQuotaBehavior().Apply(services, config, "ALPHA");
        new SiteQuotaBehavior().Apply(services, config, "BRAVO");

        await using var sp = services.BuildServiceProvider();

        await using var scope = sp.CreateAsyncScope();
        var alphaEnforcer = scope.ServiceProvider.GetRequiredKeyedService<ISiteQuotaEnforcer>("ALPHA");
        var bravoEnforcer = scope.ServiceProvider.GetRequiredKeyedService<ISiteQuotaEnforcer>("BRAVO");

        // Exhaust ALPHA's quota
        await alphaEnforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute);
        await alphaEnforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute);

        // Verify ALPHA is exhausted
        await Assert.ThrowsAsync<SiteQuotaExceededException>(
            () => alphaEnforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute));

        // BRAVO's first call must still succeed — quota is per-site, not shared
        var bravoEx = await Record.ExceptionAsync(() =>
            bravoEnforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute));
        Assert.Null(bravoEx);
    }
}
