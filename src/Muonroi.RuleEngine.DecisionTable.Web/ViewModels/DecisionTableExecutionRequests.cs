namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

/// <summary>
/// Execution request for a decision table.
/// </summary>
public sealed class DecisionTableExecuteRequest
{
    /// <summary>
    /// Input facts keyed by name.
    /// </summary>
    public Dictionary<string, object?> Inputs { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Execution response for a decision table.
/// </summary>
public sealed class DecisionTableExecuteResponse
{
    /// <summary>
    /// Whether any row matched.
    /// </summary>
    public bool Matched { get; init; }
    /// <summary>
    /// Hit policy used for evaluation.
    /// </summary>
    public string HitPolicy { get; init; } = string.Empty;
    /// <summary>
    /// Evaluation time in milliseconds.
    /// </summary>
    public double EvaluationTimeMs { get; init; }
    /// <summary>
    /// Matched row identifiers.
    /// </summary>
    public IReadOnlyList<string> MatchedRowIds { get; init; } = [];
    /// <summary>
    /// Output values grouped by row.
    /// </summary>
    public IReadOnlyList<DecisionTableOutputItem> Outputs { get; init; } = [];
}

/// <summary>
/// Output values for a matched row.
/// </summary>
public sealed class DecisionTableOutputItem
{
    /// <summary>
    /// Matched row identifier.
    /// </summary>
    public string RowId { get; init; } = string.Empty;
    /// <summary>
    /// Output values keyed by column name.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Outputs { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
