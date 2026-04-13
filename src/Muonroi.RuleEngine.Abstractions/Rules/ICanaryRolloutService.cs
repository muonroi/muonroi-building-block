namespace Muonroi.RuleEngine.Abstractions.Rules;

/// <summary>
/// Provides operations for managing canary rollouts.
/// </summary>
public interface ICanaryRolloutService
{
    /// <summary>Starts a canary rollout for a ruleset version.</summary>
    /// <param name="request">The canary rollout request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created rollout record.</returns>
    Task<CanaryRolloutRecord> StartCanaryAsync(
        StartCanaryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Promotes an active canary rollout.</summary>
    /// <param name="rolloutId">Rollout identifier.</param>
    /// <param name="promotedBy">Actor performing the promotion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PromoteCanaryAsync(
        Guid rolloutId,
        string promotedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Rolls back an active canary rollout.</summary>
    /// <param name="rolloutId">Rollout identifier.</param>
    /// <param name="rolledBackBy">Actor performing the rollback.</param>
    /// <param name="reason">Reason for rollback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RollbackCanaryAsync(
        Guid rolloutId,
        string rolledBackBy,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Returns whether a canary version is active for the tenant.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a canary version applies.</returns>
    Task<bool> IsCanaryActiveForTenantAsync(
        string workflowName,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the canary version for a tenant if targeted.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The canary version or <c>null</c>.</returns>
    Task<int?> GetCanaryVersionForTenantAsync(
        string workflowName,
        string tenantId,
        CancellationToken cancellationToken = default);
}

