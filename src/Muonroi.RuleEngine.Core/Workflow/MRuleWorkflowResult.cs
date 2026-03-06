namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Result of a workflow execution.
/// </summary>
/// <typeparam name="TContext">Workflow context type.</typeparam>
public sealed class MRuleWorkflowResult<TContext>
{
    public required string WorkflowName { get; init; }
    public required TContext Context { get; init; }
    public required FactBag Facts { get; init; }
    public required IReadOnlyList<string> ExecutedSteps { get; init; }
    public required IReadOnlyDictionary<string, object?> State { get; init; }
}

