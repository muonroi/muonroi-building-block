namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTableExecutionResult
{
    public bool Matched { get; init; }
    public IReadOnlyList<DecisionTableOutputRow> Outputs { get; init; } = [];
    public HitPolicy HitPolicy { get; init; }
    public TimeSpan EvaluationTime { get; init; }
    public IReadOnlyList<string> MatchedRowIds { get; init; } = [];
}

public sealed class DecisionTableOutputRow
{
    public string RowId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Outputs { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
