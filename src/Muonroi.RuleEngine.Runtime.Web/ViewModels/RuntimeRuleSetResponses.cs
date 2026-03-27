namespace Muonroi.RuleEngine.Runtime.Web.ViewModels;

/// <summary>Summary of workflow versions for runtime rulesets.</summary>
public sealed class RuleSetWorkflowSummary
{
    /// <summary>Workflow name.</summary>
    public required string WorkflowName { get; init; }
    /// <summary>Currently active workflow version, if any.</summary>
    public int? ActiveVersion { get; init; }
    /// <summary>Available workflow versions.</summary>
    public IReadOnlyList<int> Versions { get; init; } = [];
}

/// <summary>Rich version item returned by the versions endpoint.</summary>
public sealed class RuleSetVersionItem
{
    /// <summary>Version number.</summary>
    public required int Version { get; init; }
    /// <summary>Lifecycle status (Draft, PendingApproval, Approved, Active, Superseded, etc.).</summary>
    public required string Status { get; init; }
    /// <summary>Whether this version is the active version.</summary>
    public bool IsActive { get; init; }
    /// <summary>When this version was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Response returned after saving a ruleset.</summary>
public sealed class RuleSetSaveResponse
{
    /// <summary>Workflow name.</summary>
    public required string WorkflowName { get; init; }
    /// <summary>Version that was saved.</summary>
    public required int SavedVersion { get; init; }
    /// <summary>Active version after the save, if any.</summary>
    public int? ActiveVersion { get; init; }
    /// <summary>Whether the ruleset was activated.</summary>
    public bool Activated { get; init; }
}

/// <summary>Response returned when exporting a ruleset.</summary>
public sealed class RuleSetExportResponse
{
    /// <summary>Workflow name.</summary>
    public required string WorkflowName { get; init; }
    /// <summary>Version that was exported, if specified.</summary>
    public int? Version { get; init; }
    /// <summary>Serialized ruleset payload.</summary>
    public required string RuleSetJson { get; init; }
    /// <summary>Optional validation shape payload.</summary>
    public string? ValidationShape { get; init; }
}

/// <summary>Response returned after a dry-run execution.</summary>
public sealed class RuleSetDryRunResponse
{
    /// <summary>Indicates whether the dry run succeeded.</summary>
    public bool IsSuccess { get; init; }
    /// <summary>Facts captured during the run.</summary>
    public IReadOnlyDictionary<string, object?> Facts { get; init; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Validation or execution errors.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}
