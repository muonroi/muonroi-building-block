namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Immutable workflow definition for orchestrating rule and service tasks.
/// </summary>
/// <typeparam name="TContext">Workflow context type.</typeparam>
public sealed class MRuleWorkflowDefinition<TContext>
{
    private readonly IReadOnlyDictionary<string, MRuleWorkflowStep<TContext>> _steps;

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

    public string Name { get; }
    public string StartStepId { get; }
    public IReadOnlyDictionary<string, MRuleWorkflowStep<TContext>> Steps => _steps;
}

