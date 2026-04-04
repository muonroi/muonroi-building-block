namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Evaluates JSON defined rules using the Microsoft RulesEngine library.
/// </summary>
/// <typeparam name="TContext">Type of the context object.</typeparam>
public sealed class ExternalJsonRule<TContext> : IBusinessRule<TContext>
{
    private readonly RulesEngine.RulesEngine _engine;
    private readonly string _workflowName;

    /// <summary>Creates a JSON-backed business rule evaluator.</summary>
    /// <param name="json">RulesEngine workflow JSON.</param>
    /// <param name="workflowName">Workflow name to execute.</param>
    /// <param name="settings">Optional RulesEngine settings.</param>
    public ExternalJsonRule(string json, string workflowName, ReSettings? settings = null)
    {
        Workflow[] workflows = JsonSerializer.Deserialize<Workflow[]>(json) ?? []; // MBB002-exempt: constructor-injected string — Workflow type requires direct JsonSerializer
        _engine = new RulesEngine.RulesEngine(workflows, settings ?? new ReSettings());
        _workflowName = workflowName;
        Code = workflowName;
    }

    /// <summary>Gets the rule code or identifier.</summary>
    public string Code { get; }

    /// <summary>Evaluates the rule against the provided context.</summary>
    /// <param name="context">Context input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if all rules succeed.</returns>
    public async Task<bool> IsSatisfiedAsync(TContext context, CancellationToken cancellationToken = default)
    {
        dynamic[] inputs = [new { value = context }];
        List<RuleResultTree> results = await _engine.ExecuteAllRulesAsync(_workflowName, inputs);
        return results.All(r => r.IsSuccess);
    }
}
