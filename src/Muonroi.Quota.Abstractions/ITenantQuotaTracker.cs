namespace Muonroi.Quota.Abstractions;

/// <summary>
/// Provides methods for tracking and managing tenant-specific usage quotas.
/// </summary>
public interface ITenantQuotaTracker
{
    /// <summary>
    /// Checks if the specified tenant has enough quota remaining for the given action.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="type">The type of quota to check.</param>
    /// <param name="amount">The amount of quota being requested. Default is 1.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns><c>true</c> if the tenant has enough quota; otherwise, <c>false</c>.</returns>
    Task<bool> CheckQuotaAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default);

    /// <summary>
    /// Increments the usage for a specific quota type for the given tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="type">The type of quota to increment.</param>
    /// <param name="amount">The amount of usage to add. Default is 1.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task IncrementUsageAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current quota usage statistics for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="QuotaUsage"/> object containing usage statistics.</returns>
    Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Resets all daily quotas to zero.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResetDailyQuotasAsync(CancellationToken ct = default);
}
