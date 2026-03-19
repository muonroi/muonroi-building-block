namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Result of executing a sub-flow via <c>RulesEngineService.ExecuteSubFlowAsync</c>.
/// </summary>
public sealed class SubFlowExecutionResult
{
    public bool IsSuccess { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public FactBag OutputFacts { get; init; } = new();
}
