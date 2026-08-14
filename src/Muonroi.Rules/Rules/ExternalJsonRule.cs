namespace Muonroi.Rules.Rules;

/// <summary>
/// Evaluates JSON defined rules using the Microsoft RulesEngine library.
/// </summary>
/// <typeparam name="TContext">Type of the context object.</typeparam>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public sealed class ExternalJsonRule<TContext> : IBusinessRule<TContext>
{
    private readonly RulesEngine.RulesEngine _engine;
    private readonly string _workflowName;

    /// <summary>
    /// Evaluates JSON defined rules using the Microsoft RulesEngine library.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="workflowName"></param>
    /// <param name="settings"></param>
    public ExternalJsonRule(string json, string workflowName, ReSettings? settings = null)
    {
        Workflow[] workflows = JsonSerializer.Deserialize<Workflow[]>(json) ?? []; // MBB002-exempt: constructor-injected string — Workflow type requires direct JsonSerializer
        _engine = new RulesEngine.RulesEngine(workflows, settings ?? new ReSettings());
        _workflowName = workflowName;
        Code = workflowName;
    }

    /// <summary>
    /// Code of the rule, which is the workflow name in this case.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// is satisfied if all rules in the specified workflow evaluate to true for the given context.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> IsSatisfiedAsync(TContext context, CancellationToken cancellationToken = default)
    {
        dynamic[] inputs = [new { value = context }];
        List<RuleResultTree> results = await _engine.ExecuteAllRulesAsync(_workflowName, inputs);
        return results.All(r => r.IsSuccess);
    }
}
