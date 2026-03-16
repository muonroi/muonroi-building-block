using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Tenancy.Abstractions;

public sealed class InMemoryTenantQuotaTracker(ITenantQuotaStore quotaStore) : ITenantQuotaTracker
{
    public async Task<bool> CheckQuotaAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default)
    {
        TenantQuota quota = await quotaStore.GetQuotaAsync(tenantId, ct) ?? TenantQuotaPresets.Free;
        QuotaUsage usage = await quotaStore.GetUsageAsync(tenantId, ct);

        usage.CurrentUsage.TryGetValue(type, out int current);
        int limit = GetLimit(quota, type);
        if (limit == int.MaxValue)
        {
            return true;
        }

        return current + amount <= limit;
    }

    public Task IncrementUsageAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default)
    {
        return quotaStore.RecordUsageAsync(tenantId, type, amount, ct);
    }

    public Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default)
    {
        return quotaStore.GetUsageAsync(tenantId, ct);
    }

    public Task ResetDailyQuotasAsync(CancellationToken ct = default)
    {
        return quotaStore.ResetDailyCountersAsync(ct);
    }

    private static int GetLimit(TenantQuota quota, QuotaType type)
    {
        return type switch
        {
            QuotaType.RuleExecutionsPerDay => quota.MaxRuleExecutionsPerDay,
            QuotaType.ConcurrentExecutions => quota.MaxConcurrentExecutions,
            QuotaType.ApiRequestsPerMinute => quota.MaxApiRequestsPerMinute,
            QuotaType.RuleEvaluationsPerSecond => quota.MaxRuleEvaluationsPerSecond,
            QuotaType.WorkflowExecutionsPerHour => quota.MaxWorkflowExecutionsPerHour,
            QuotaType.StorageUsageMB => quota.MaxStorageMB,
            QuotaType.TotalRules => quota.MaxRulesPerTenant,
            QuotaType.TotalDecisionTables => quota.MaxDecisionTables,
            QuotaType.TotalWorkflows => quota.MaxJsonWorkflows,
            QuotaType.TotalConnectors => quota.MaxTotalConnectors,
            QuotaType.ConnectorExecutionsPerDay => quota.MaxConnectorExecutionsPerDay,
            _ => int.MaxValue
        };
    }
}
