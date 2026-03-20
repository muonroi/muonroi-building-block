using Microsoft.AspNetCore.Mvc;

namespace Muonroi.Rules.Controllers;

/// <summary>
/// Controller for DMN FEEL (Friendly Enough Expression Language) operations.
/// </summary>
[ApiController]
[Route("api/v1/feel")]
public sealed class FeelController : FeelControllerBase
{
}

/// <summary>
/// Represents a request to evaluate a FEEL expression.
/// </summary>
public sealed record FeelEvaluateRequest
{
    /// <summary>
    /// The FEEL expression to evaluate.
    /// </summary>
    public string Expression { get; init; } = string.Empty;
    /// <summary>
    /// The context (variables) to use for evaluation.
    /// </summary>
    public Dictionary<string, JsonElement> Context { get; init; } = [];
}

/// <summary>
/// Represents a request for FEEL expression autocompletion.
/// </summary>
public sealed record FeelAutocompleteRequest
{
    /// <summary>
    /// The partial FEEL expression typed so far.
    /// </summary>
    public string PartialExpression { get; init; } = string.Empty;
    /// <summary>
    /// Optional data type context for autocompletion.
    /// </summary>
    public string? DataType { get; init; }
    /// <summary>
    /// Optional context (variables) to use for autocompletion suggestions.
    /// </summary>
    public Dictionary<string, JsonElement>? Context { get; init; }
}
