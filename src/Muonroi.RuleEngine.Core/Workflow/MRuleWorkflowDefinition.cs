namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Immutable workflow definition for orchestrating rule and service tasks.
/// </summary>
/// <typeparam name="TContext">Workflow context type.</typeparam>
public sealed class MRuleWorkflowDefinition<TContext>
{
    private readonly IReadOnlyDictionary<string, MRuleWorkflowStep<TContext>> _steps;

    /// <summary>
    /// Initializes a new instance of the <see cref="MRuleWorkflowDefinition{TContext}"/> class.
    /// </summary>
    /// <param name="name">The name of the workflow.</param>
    /// <param name="startStepId">The identifier of the first step in the workflow.</param>
    /// <param name="steps">The sequence of steps in the workflow.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="startStepId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="steps"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there are duplicate step IDs, no steps, or the start step is missing.</exception>
    public MRuleWorkflowDefinition(
        string name,
        string startStepId,
        IEnumerable<MRuleWorkflowStep<TContext>> steps)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Workflow name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(startStepId))
        {
            throw new ArgumentException("Start step id is required.", nameof(startStepId));
        }

        Dictionary<string, MRuleWorkflowStep<TContext>> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (MRuleWorkflowStep<TContext> step in steps ?? throw new ArgumentNullException(nameof(steps)))
        {
            if (!map.TryAdd(step.Id, step))
            {
                throw new InvalidOperationException($"Duplicate workflow step id '{step.Id}'.");
            }
        }

        if (map.Count == 0)
        {
            throw new InvalidOperationException("Workflow must contain at least one step.");
        }

        if (!map.ContainsKey(startStepId))
        {
            throw new InvalidOperationException($"Start step '{startStepId}' was not found in workflow steps.");
        }

        Name = name;
        StartStepId = startStepId;
        _steps = map;
    }

    /// <summary>
    /// Gets the name of the workflow.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the identifier of the starting step.
    /// </summary>
    public string StartStepId { get; }

    /// <summary>
    /// Gets the read-only dictionary of steps in the workflow, keyed by their identifier.
    /// </summary>
    public IReadOnlyDictionary<string, MRuleWorkflowStep<TContext>> Steps => _steps;
}

