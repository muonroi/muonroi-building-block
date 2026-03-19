using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Tenancy.Core.Shared;

public sealed class InMemoryTenantQuotaStore(IMDateTimeService dateTimeService, IMJsonSerializeService jsonSerializeService) : ITenantQuotaStore
{
    private readonly ConcurrentDictionary<string, TenantQuota> _quotas = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<QuotaType, int>> _usage = new(StringComparer.OrdinalIgnoreCase);

    public Task<TenantQuota?> GetQuotaAsync(string tenantId, CancellationToken ct = default)
    {
        _quotas.TryGetValue(tenantId, out TenantQuota? quota);
        return Task.FromResult(quota is null ? null : Clone(quota));
    }

    public Task SaveQuotaAsync(string tenantId, TenantQuota quota, CancellationToken ct = default)
    {
        quota.TenantId = tenantId;
        _quotas[tenantId] = Clone(quota);
        return Task.CompletedTask;
    }

    public Task RecordUsageAsync(string tenantId, QuotaType type, int amount, CancellationToken ct = default)
    {
        ConcurrentDictionary<QuotaType, int> bucket = _usage.GetOrAdd(tenantId, _ => new ConcurrentDictionary<QuotaType, int>());
        bucket.AddOrUpdate(type, amount, (_, current) => current + amount);
        return Task.CompletedTask;
    }

    public async Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default)
    {
        TenantQuota quota = await GetQuotaAsync(tenantId, ct) ?? TenantQuotaPresets.Free;
        ConcurrentDictionary<QuotaType, int> bucket = _usage.GetOrAdd(tenantId, _ => new ConcurrentDictionary<QuotaType, int>());
        QuotaUsage async = new()
        {
            TenantId = tenantId,
            CurrentUsage = bucket.ToDictionary(x => x.Key, x => x.Value),
            Limits = BuildLimits(quota),
            PeriodStart = dateTimeService.UtcNow().Date,
            PeriodEnd = dateTimeService.UtcNow().Date.AddDays(1).AddTicks(-1)
        };
        return async;
    }

    public Task ResetDailyCountersAsync(CancellationToken ct = default)
    {
        _usage.Clear();
        return Task.CompletedTask;
    }

    private TenantQuota Clone(TenantQuota source)
    {
        string json = jsonSerializeService.Serialize(source);
        return jsonSerializeService.Deserialize<TenantQuota>(json) ?? new TenantQuota();
    }

    private static Dictionary<QuotaType, int> BuildLimits(TenantQuota quota)
    {
        Dictionary<QuotaType, int> ints = new()
        {
            [QuotaType.RuleExecutionsPerDay] = quota.MaxRuleExecutionsPerDay,
            [QuotaType.ConcurrentExecutions] = quota.MaxConcurrentExecutions,
            [QuotaType.ApiRequestsPerMinute] = quota.MaxApiRequestsPerMinute,
            [QuotaType.RuleEvaluationsPerSecond] = quota.MaxRuleEvaluationsPerSecond,
            [QuotaType.WorkflowExecutionsPerHour] = quota.MaxWorkflowExecutionsPerHour,
            [QuotaType.StorageUsageMB] = quota.MaxStorageMB,
            [QuotaType.TotalRules] = quota.MaxRulesPerTenant,
            [QuotaType.TotalDecisionTables] = quota.MaxDecisionTables,
            [QuotaType.TotalWorkflows] = quota.MaxJsonWorkflows,
            [QuotaType.TotalConnectors] = quota.MaxTotalConnectors,
            [QuotaType.ConnectorExecutionsPerDay] = quota.MaxConnectorExecutionsPerDay
        };
        return ints;
    }
}
