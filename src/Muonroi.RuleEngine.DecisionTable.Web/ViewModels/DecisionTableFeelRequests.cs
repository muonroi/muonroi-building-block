namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

public sealed class FeelValidateRequest
{
    public string Expression { get; init; } = string.Empty;
    public string? ColumnDataType { get; init; }
}

public sealed class FeelValidateResponse
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
}
