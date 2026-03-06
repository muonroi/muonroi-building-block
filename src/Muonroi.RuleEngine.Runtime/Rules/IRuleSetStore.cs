


namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Abstraction for persisting and retrieving versioned ruleset definitions.
/// </summary>
public interface IRuleSetStore
{
    /// <summary>Saves the provided ruleset as a new version.</summary>
    Task SaveAsync(string workflowName, string json, CancellationToken cancellationToken = default);

    /// <summary>Gets the ruleset JSON for the specified workflow and version. When version is null, the active version is returned.</summary>
    Task<string?> GetAsync(string workflowName, int? version = null, CancellationToken cancellationToken = default);

    /// <summary>Sets the active version for a workflow.</summary>
    Task SetActiveVersionAsync(string workflowName, int version, CancellationToken cancellationToken = default);

    /// <summary>Lists all available versions for a workflow.</summary>
    Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active version for a workflow when supported by the backing store.
    /// Implementations that do not persist active metadata can return <c>null</c>.
    /// </summary>
    Task<int?> GetActiveVersionAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        _ = workflowName;
        _ = cancellationToken;
        return Task.FromResult<int?>(null);
    }

    /// <summary>
    /// Returns all workflow names visible to the current tenant scope when supported by the backing store.
    /// </summary>
    Task<IReadOnlyList<string>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
