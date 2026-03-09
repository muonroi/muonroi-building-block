namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

public sealed class DecisionTableExecuteRequest
{
    public Dictionary<string, object?> Inputs { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DecisionTableExecuteResponse
{
    public bool Matched { get; init; }
    public string HitPolicy { get; init; } = string.Empty;
    public double EvaluationTimeMs { get; init; }
    public IReadOnlyList<string> MatchedRowIds { get; init; } = [];
    public IReadOnlyList<DecisionTableOutputItem> Outputs { get; init; } = [];
}

public sealed class DecisionTableOutputItem
{
    public string RowId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Outputs { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
