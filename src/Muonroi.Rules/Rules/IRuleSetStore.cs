


namespace Muonroi.Rules.Rules;

/// <summary>
/// Abstraction for persisting and retrieving versioned ruleset definitions.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
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
}
