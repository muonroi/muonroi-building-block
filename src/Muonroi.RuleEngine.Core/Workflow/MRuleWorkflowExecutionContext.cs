namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Mutable runtime context shared across all workflow steps.
/// </summary>
/// <typeparam name="TContext">Workflow context type.</typeparam>
public sealed class MRuleWorkflowExecutionContext<TContext>(
    TContext context,
    FactBag facts,
    IMRuleExecutionRouter<TContext> ruleRouter)
{
    private readonly Dictionary<string, object?> _state = [];

    /// <summary>
    /// Business context passed to rule/service steps.
    /// </summary>
    public TContext Context { get; } = context;

    /// <summary>
    /// Shared facts produced by rule tasks and service tasks.
    /// </summary>
    public FactBag Facts { get; } = facts;

    /// <summary>
    /// Rule router used by workflow rule tasks.
    /// </summary>
    public IMRuleExecutionRouter<TContext> RuleRouter { get; } = ruleRouter;

    /// <summary>
    /// Current step id being executed.
    /// </summary>
    public string CurrentStepId { get; internal set; } = string.Empty;

    /// <summary>
    /// Read-only workflow runtime state.
    /// </summary>
    public IReadOnlyDictionary<string, object?> State => _state;

    /// <summary>
    /// Sets state value for cross-step coordination.
    /// </summary>
    public void SetState(string key, object? value)
    {
        _state[key] = value;
    }

    /// <summary>
    /// Reads state value for cross-step coordination.
    /// </summary>
    public T? GetState<T>(string key)
    {
        if (!_state.TryGetValue(key, out object? value) || value is null)
        {
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}

