namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Runtime cache for ruleset payloads to reduce storage reads.
/// </summary>
public interface IRuleSetRuntimeCache
{
    /// <summary>Gets a cached ruleset or creates it using the factory.</summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="factory">Factory to load ruleset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached ruleset content.</returns>
    Task<string?> GetOrCreateAsync(
        string tenantId,
        string workflowName,
        Func<Task<string?>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>Invalidates cached ruleset data for a workflow.</summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateAsync(string tenantId, string workflowName, CancellationToken cancellationToken = default);
}
