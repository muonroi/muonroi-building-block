namespace Muonroi.RuleEngine.Runtime.Web.ViewModels;

public sealed class RuleSetWorkflowSummary
{
    public required string WorkflowName { get; init; }
    public int? ActiveVersion { get; init; }
    public IReadOnlyList<int> Versions { get; init; } = [];
}

public sealed class RuleSetSaveResponse
{
    public required string WorkflowName { get; init; }
    public required int SavedVersion { get; init; }
    public int? ActiveVersion { get; init; }
    public bool Activated { get; init; }
}

public sealed class RuleSetExportResponse
{
    public required string WorkflowName { get; init; }
    public int? Version { get; init; }
    public required string RuleSetJson { get; init; }
    public string? ValidationShape { get; init; }
}

public sealed class RuleSetDryRunResponse
{
    public bool IsSuccess { get; init; }
    public IReadOnlyDictionary<string, object?> Facts { get; init; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> Errors { get; init; } = [];
}
