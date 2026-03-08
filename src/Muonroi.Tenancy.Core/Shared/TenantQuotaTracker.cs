using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Tenancy.Core.Shared;

public sealed class TenantQuotaTracker(
    IDistributedCache cache,
    ITenantQuotaStore quotaStore,
    IMLog<TenantQuotaTracker> logger,
    IMDateTimeService dateTimeService) : ITenantQuotaTracker
{
    public async Task<bool> CheckQuotaAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default)
    {
        TenantQuota? quota = await quotaStore.GetQuotaAsync(tenantId, ct);
        if (quota is null)
        {
            logger.LogWarning("No quota found for tenant {TenantId}, defaulting to Free tier.", tenantId);
            quota = TenantQuotaPresets.Free;
        }

        int usage = await GetCurrentUsageAsync(tenantId, type, ct);
        int limit = GetLimit(quota, type);
        if (limit == int.MaxValue)
        {
            return true;
        }

        return usage + amount <= limit;
    }

    public async Task IncrementUsageAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default)
    {
        string key = GetCacheKey(tenantId, type);
        string? currentRaw = await cache.GetStringAsync(key, ct);
        int current = int.TryParse(currentRaw, out int parsed) ? parsed : 0;
        int updated = Math.Max(0, current + amount);

        await cache.SetStringAsync(key, updated.ToString(CultureInfo.InvariantCulture), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = GetExpirationFor(type)
        }, ct);

        await quotaStore.RecordUsageAsync(tenantId, type, amount, ct);
    }

    public Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default)
    {
        return quotaStore.GetUsageAsync(tenantId, ct);
    }

    public Task ResetDailyQuotasAsync(CancellationToken ct = default)
    {
        return quotaStore.ResetDailyCountersAsync(ct);
    }

    private async Task<int> GetCurrentUsageAsync(string tenantId, QuotaType type, CancellationToken ct)
    {
        string? raw = await cache.GetStringAsync(GetCacheKey(tenantId, type), ct);
        return int.TryParse(raw, out int usage) ? usage : 0;
    }

    private string GetCacheKey(string tenantId, QuotaType type)
    {
        DateTime utcNow = dateTimeService.UtcNow();
        string periodKey = type switch
        {
            QuotaType.RuleEvaluationsPerSecond => utcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            QuotaType.ApiRequestsPerMinute => utcNow.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture),
            QuotaType.WorkflowExecutionsPerHour => utcNow.ToString("yyyyMMddHH", CultureInfo.InvariantCulture),
            _ => utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
        };

        return $"quota:{tenantId}:{type}:{periodKey}";
    }

    private static TimeSpan GetExpirationFor(QuotaType type)
    {
        return type switch
        {
            QuotaType.RuleEvaluationsPerSecond => TimeSpan.FromSeconds(2),
            QuotaType.ApiRequestsPerMinute => TimeSpan.FromMinutes(2),
            QuotaType.WorkflowExecutionsPerHour => TimeSpan.FromHours(2),
            _ => TimeSpan.FromDays(2)
        };
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
            _ => int.MaxValue
        };
    }
}
