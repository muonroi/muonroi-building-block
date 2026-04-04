namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Result of a workflow execution.
/// </summary>
/// <typeparam name="TContext">Workflow context type.</typeparam>
public sealed class MRuleWorkflowResult<TContext>
{
    /// <summary>
    /// Gets the name of the executed workflow.
    /// </summary>
    public required string WorkflowName { get; init; }

    /// <summary>
    /// Gets the final state of the workflow context.
    /// </summary>
    public required TContext Context { get; init; }

    /// <summary>
    /// Gets the collection of facts accumulated during execution.
    /// </summary>
    public required FactBag Facts { get; init; }

    /// <summary>
    /// Gets the ordered list of identifiers for the steps that were executed.
    /// </summary>
    public required IReadOnlyList<string> ExecutedSteps { get; init; }

    /// <summary>
    /// Gets the state accumulated during the workflow execution.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> State { get; init; }
}

