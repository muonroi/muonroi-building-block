namespace Muonroi.RuleEngine.Abstractions;

public interface ITenantQuotaTracker
{
    Task<bool> CheckQuotaAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default);
    Task IncrementUsageAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default);
    Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default);
    Task ResetDailyQuotasAsync(CancellationToken ct = default);
}
