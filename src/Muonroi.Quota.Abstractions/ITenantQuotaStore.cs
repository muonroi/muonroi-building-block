namespace Muonroi.Quota.Abstractions;

/// <summary>
/// Defines the contract for storing and retrieving tenant quotas and usage information.
/// </summary>
public interface ITenantQuotaStore
{
    /// <summary>
    /// Gets the quota for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the tenant quota, or null if not found.</returns>
    Task<TenantQuota?> GetQuotaAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Saves the quota for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="quota">The tenant quota to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveQuotaAsync(string tenantId, TenantQuota quota, CancellationToken ct = default);

    /// <summary>
    /// Records usage for a specific quota type for a tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="type">The type of quota to record usage for.</param>
    /// <param name="amount">The amount of usage to record.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RecordUsageAsync(string tenantId, QuotaType type, int amount, CancellationToken ct = default);

    /// <summary>
    /// Gets the current usage information for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the current quota usage.</returns>
    Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Resets daily usage counters for all tenants.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ResetDailyCountersAsync(CancellationToken ct = default);
}
