namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Result of a decision table execution.
/// </summary>
public sealed class DecisionTableExecutionResult
{
    /// <summary>True when at least one row matched.</summary>
    public bool Matched { get; init; }
    /// <summary>Output rows produced by matches.</summary>
    public IReadOnlyList<DecisionTableOutputRow> Outputs { get; init; } = [];
    /// <summary>Hit policy applied during evaluation.</summary>
    public HitPolicy HitPolicy { get; init; }
    /// <summary>Total evaluation time.</summary>
    public TimeSpan EvaluationTime { get; init; }
    /// <summary>Identifiers of matched rows.</summary>
    public IReadOnlyList<string> MatchedRowIds { get; init; } = [];
}

/// <summary>
/// Output row produced by a decision table match.
/// </summary>
public sealed class DecisionTableOutputRow
{
    /// <summary>Identifier of the matched row.</summary>
    public string RowId { get; init; } = string.Empty;
    /// <summary>Output values keyed by column name.</summary>
    public IReadOnlyDictionary<string, object?> Outputs { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
