using Muonroi.Tenancy.Abstractions.Interfaces;
using Muonroi.Tenancy.Abstractions.Models;
using System.Globalization;

namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Stores tenant quota data in an EF Core context.
/// </summary>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
/// <param name="context">The database context.</param>
/// <param name="dateTimeService">Date/time service for period calculation.</param>
public sealed class EfTenantQuotaStore<TContext>(TContext context, IMDateTimeService dateTimeService) : ITenantQuotaStore
    where TContext : MDbContext
{
    /// <summary>Gets the quota configuration for a tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tenant quota, or <c>null</c> if not found.</returns>
    public async Task<TenantQuota?> GetQuotaAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);

        MTenantQuota? entity = await context.TenantQuotas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        return entity is null ? null : MapToModel(entity);
    }

    /// <summary>Persists the quota configuration for a tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="quota">The quota definition to save.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveQuotaAsync(string tenantId, TenantQuota quota, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(quota);

        MTenantQuota? entity = await context.TenantQuotas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        if (entity is null)
        {
            entity = new MTenantQuota
            {
                TenantId = tenantId
            };
            await context.TenantQuotas.AddAsync(entity, ct);
        }

        entity.Tier = quota.Tier;
        entity.MaxRulesPerTenant = quota.MaxRulesPerTenant;
        entity.MaxRuleExecutionsPerDay = quota.MaxRuleExecutionsPerDay;
        entity.MaxConcurrentExecutions = quota.MaxConcurrentExecutions;
        entity.MaxDecisionTables = quota.MaxDecisionTables;
        entity.MaxJsonWorkflows = quota.MaxJsonWorkflows;
        entity.MaxStorageMB = quota.MaxStorageMB;
        entity.MaxApiRequestsPerMinute = quota.MaxApiRequestsPerMinute;
        entity.MaxRuleEvaluationsPerSecond = quota.MaxRuleEvaluationsPerSecond;
        entity.MaxWorkflowExecutionsPerHour = quota.MaxWorkflowExecutionsPerHour;
        entity.MaxRuleComplexity = quota.MaxRuleComplexity;
        entity.MaxWorkflowSizeKB = quota.MaxWorkflowSizeKB;
        entity.MaxExecutionTimeMs = quota.MaxExecutionTimeMs;
        entity.MaxTotalConnectors = quota.MaxTotalConnectors;
        entity.MaxConnectorExecutionsPerDay = quota.MaxConnectorExecutionsPerDay;

        await context.SaveChangesAsync(ct);
    }

    /// <summary>Records usage for a quota bucket.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="type">The quota type being tracked.</param>
    /// <param name="amount">The amount to add (or subtract).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RecordUsageAsync(string tenantId, QuotaType type, int amount, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        if (amount == 0)
        {
            return;
        }

        (string period, DateTime periodStart, DateTime periodEnd) = GetPeriodBucket(type, dateTimeService.UtcNow());
        string quotaType = type.ToString();

        MTenantQuotaUsage? entity = await context.TenantQuotaUsages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.QuotaType == quotaType && x.Period == period, ct);

        if (entity is null)
        {
            entity = new MTenantQuotaUsage
            {
                TenantId = tenantId,
                QuotaType = quotaType,
                Period = period,
                Amount = Math.Max(0, amount),
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };
            await context.TenantQuotaUsages.AddAsync(entity, ct);
        }
        else
        {
            entity.Amount = Math.Max(0, entity.Amount + amount);
            entity.PeriodStart = periodStart;
            entity.PeriodEnd = periodEnd;
        }

        await context.SaveChangesAsync(ct);
    }

    /// <summary>Gets current quota usage for a tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current quota usage snapshot.</returns>
    public async Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);

        DateTime now = dateTimeService.UtcNow();
        TenantQuota quota = await GetQuotaAsync(tenantId, ct) ?? TenantQuotaPresets.Free;
        List<MTenantQuotaUsage> activeRows = await context.TenantQuotaUsages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PeriodStart <= now && x.PeriodEnd >= now)
            .ToListAsync(ct);

        Dictionary<QuotaType, int> usage = Enum.GetValues<QuotaType>()
            .ToDictionary(static type => type, _ => 0);

        foreach (MTenantQuotaUsage row in activeRows)
        {
            if (!Enum.TryParse(row.QuotaType, ignoreCase: true, out QuotaType parsed))
            {
                continue;
            }

            usage[parsed] = row.Amount;
        }

        return new QuotaUsage
        {
            TenantId = tenantId,
            CurrentUsage = usage,
            Limits = BuildLimits(quota),
            PeriodStart = now.Date,
            PeriodEnd = now.Date.AddDays(1).AddTicks(-1)
        };
    }

    /// <summary>Removes stale usage rows that are outside the current day.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResetDailyCountersAsync(CancellationToken ct = default)
    {
        DateTime todayUtc = dateTimeService.UtcNow().Date;
        List<MTenantQuotaUsage> staleRows = await context.TenantQuotaUsages
            .IgnoreQueryFilters()
            .Where(x => x.PeriodEnd < todayUtc)
            .ToListAsync(ct);

        if (staleRows.Count == 0)
        {
            return;
        }

        context.TenantQuotaUsages.RemoveRange(staleRows);
        await context.SaveChangesAsync(ct);
    }

    private static TenantQuota MapToModel(MTenantQuota entity)
    {
        return new TenantQuota
        {
            TenantId = entity.TenantId,
            Tier = entity.Tier,
            MaxRulesPerTenant = entity.MaxRulesPerTenant,
            MaxRuleExecutionsPerDay = entity.MaxRuleExecutionsPerDay,
            MaxConcurrentExecutions = entity.MaxConcurrentExecutions,
            MaxDecisionTables = entity.MaxDecisionTables,
            MaxJsonWorkflows = entity.MaxJsonWorkflows,
            MaxStorageMB = entity.MaxStorageMB,
            MaxApiRequestsPerMinute = entity.MaxApiRequestsPerMinute,
            MaxRuleEvaluationsPerSecond = entity.MaxRuleEvaluationsPerSecond,
            MaxWorkflowExecutionsPerHour = entity.MaxWorkflowExecutionsPerHour,
            MaxRuleComplexity = entity.MaxRuleComplexity,
            MaxWorkflowSizeKB = entity.MaxWorkflowSizeKB,
            MaxExecutionTimeMs = entity.MaxExecutionTimeMs,
            MaxTotalConnectors = entity.MaxTotalConnectors,
            MaxConnectorExecutionsPerDay = entity.MaxConnectorExecutionsPerDay
        };
    }

    private static (string Period, DateTime PeriodStart, DateTime PeriodEnd) GetPeriodBucket(QuotaType type, DateTime now)
    {
        return type switch
        {
            QuotaType.RuleEvaluationsPerSecond => CreatePeriod(now, "yyyyMMddHHmmss", static dt => dt.AddSeconds(1)),
            QuotaType.ApiRequestsPerMinute => CreatePeriod(now, "yyyyMMddHHmm", static dt => dt.AddMinutes(1)),
            QuotaType.WorkflowExecutionsPerHour => CreatePeriod(now, "yyyyMMddHH", static dt => dt.AddHours(1)),
            _ => CreatePeriod(now, "yyyyMMdd", static dt => dt.AddDays(1))
        };
    }

    private static (string Period, DateTime PeriodStart, DateTime PeriodEnd) CreatePeriod(
        DateTime now,
        string periodFormat,
        Func<DateTime, DateTime> nextPeriod)
    {
        DateTime periodStart = periodFormat switch
        {
            "yyyyMMddHHmmss" => new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc),
            "yyyyMMddHHmm" => new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc),
            "yyyyMMddHH" => new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc),
            _ => now.Date
        };

        DateTime periodEnd = nextPeriod(periodStart).AddTicks(-1);
        return (periodStart.ToString(periodFormat, CultureInfo.InvariantCulture), periodStart, periodEnd);
    }

    private static Dictionary<QuotaType, int> BuildLimits(TenantQuota quota)
    {
        return new Dictionary<QuotaType, int>
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
    }
}
