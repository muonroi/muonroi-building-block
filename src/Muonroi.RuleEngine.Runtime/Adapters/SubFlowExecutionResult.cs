namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Result of executing a sub-flow via <c>RulesEngineService.ExecuteSubFlowAsync</c>.
/// </summary>
public sealed class SubFlowExecutionResult
{
    /// <summary>Gets a value indicating whether the sub-flow succeeded.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Gets the error messages when the sub-flow failed.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Gets the output facts from the sub-flow.</summary>
    public FactBag OutputFacts { get; init; } = new();
}
