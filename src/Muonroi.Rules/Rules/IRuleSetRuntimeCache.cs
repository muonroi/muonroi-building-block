namespace Muonroi.Rules.Rules;

/// <summary>
/// Runtime cache for ruleset payloads to reduce storage reads.
/// </summary>
public interface IRuleSetRuntimeCache
{
    /// <summary>
    /// Gets a cached ruleset payload or creates it using the provided factory.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="workflowName">The name of the workflow.</param>
    /// <param name="factory">A factory function to create the ruleset if not cached.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing the ruleset payload.</returns>
    Task<string?> GetOrCreateAsync(
        string tenantId,
        string workflowName,
        Func<Task<string?>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cache for a specific ruleset.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="workflowName">The name of the workflow.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous invalidation.</returns>
    Task InvalidateAsync(string tenantId, string workflowName, CancellationToken cancellationToken = default);
}
