namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

/// <summary>
/// FEEL validation request.
/// </summary>
public sealed class FeelValidateRequest
{
    /// <summary>
    /// FEEL expression to validate.
    /// </summary>
    public string Expression { get; init; } = string.Empty;
    /// <summary>
    /// Optional column data type hint.
    /// </summary>
    public string? ColumnDataType { get; init; }
}

/// <summary>
/// FEEL validation response.
/// </summary>
public sealed class FeelValidateResponse
{
    /// <summary>
    /// Whether the expression is valid.
    /// </summary>
    public bool IsValid { get; init; }
    /// <summary>
    /// Error message when invalid.
    /// </summary>
    public string? Error { get; init; }
}
