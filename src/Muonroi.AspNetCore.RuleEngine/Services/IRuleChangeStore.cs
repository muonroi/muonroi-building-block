namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Defines a store for managing and recording rule order changes.
/// </summary>
public interface IRuleChangeStore
{
    /// <summary>
    /// Gets the current rule order for a specific tenant and endpoint route.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="endpointRoute">The endpoint route.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of rule codes in their current order.</returns>
    Task<IReadOnlyList<string>> GetCurrentAsync(
        string tenantId,
        string endpointRoute,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a new rule order change.
    /// </summary>
    /// <param name="request">The rule order change request.</param>
    /// <param name="appliedBy">The user or system that applied the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="RuleChangeRecord"/> representing the applied change.</returns>
    Task<RuleChangeRecord> ApplyAsync(
        RuleOrderChangeRequest request,
        string appliedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the last rule order change for a specific tenant and endpoint route.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="endpointRoute">The endpoint route.</param>
    /// <param name="appliedBy">The user or system performing the rollback.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="RuleChangeRecord"/> representing the rollback state, or null if no history exists.</returns>
    Task<RuleChangeRecord?> RollbackAsync(
        string tenantId,
        string endpointRoute,
        string appliedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the history of rule order changes for a specific tenant and endpoint route.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="endpointRoute">The endpoint route.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of <see cref="RuleChangeRecord"/> objects representing the change history.</returns>
    Task<IReadOnlyList<RuleChangeRecord>> GetHistoryAsync(
        string tenantId,
        string endpointRoute,
        CancellationToken cancellationToken = default);
}
