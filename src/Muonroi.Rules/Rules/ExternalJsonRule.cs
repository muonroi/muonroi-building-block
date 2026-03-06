using RulesEngine.Models;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Evaluates JSON defined rules using the Microsoft RulesEngine library.
/// </summary>
/// <typeparam name="TContext">Type of the context object.</typeparam>
public sealed class ExternalJsonRule<TContext> : IBusinessRule<TContext>
{
    private readonly RulesEngine.RulesEngine _engine;
    private readonly string _workflowName;

    public ExternalJsonRule(string json, string workflowName, ReSettings? settings = null)
    {
        Workflow[] workflows = JsonSerializer.Deserialize<Workflow[]>(json) ?? []; // MBB002-exempt: constructor-injected string — Workflow type requires direct JsonSerializer
        _engine = new RulesEngine.RulesEngine(workflows, settings ?? new ReSettings());
        _workflowName = workflowName;
        Code = workflowName;
    }

    public string Code { get; }

    public async Task<bool> IsSatisfiedAsync(TContext context, CancellationToken cancellationToken = default)
    {
        dynamic[] inputs = [new { value = context }];
        List<RuleResultTree> results = await _engine.ExecuteAllRulesAsync(_workflowName, inputs);
        return results.All(r => r.IsSuccess);
    }
}
