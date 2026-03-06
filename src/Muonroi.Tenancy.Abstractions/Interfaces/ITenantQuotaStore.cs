using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Tenancy.Abstractions.Interfaces;

public interface ITenantQuotaStore
{
    Task<TenantQuota?> GetQuotaAsync(string tenantId, CancellationToken ct = default);
    Task SaveQuotaAsync(string tenantId, TenantQuota quota, CancellationToken ct = default);
    Task RecordUsageAsync(string tenantId, QuotaType type, int amount, CancellationToken ct = default);
    Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default);
    Task ResetDailyCountersAsync(CancellationToken ct = default);
}
