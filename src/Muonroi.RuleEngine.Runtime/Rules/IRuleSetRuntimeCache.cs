namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Runtime cache for ruleset payloads to reduce storage reads.
/// </summary>
public interface IRuleSetRuntimeCache
{
    Task<string?> GetOrCreateAsync(
        string tenantId,
        string workflowName,
        Func<Task<string?>> factory,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(string tenantId, string workflowName, CancellationToken cancellationToken = default);
}
