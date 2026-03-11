using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;
using Muonroi.Rules.Feel;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Executes a FEEL boolean expression as an <see cref="IRule{TContext}"/>.
/// Evaluates to <see cref="RuleResult.Passed()"/> when the expression is true;
/// <see cref="RuleResult.Failure(string[])"/> otherwise.
/// </summary>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
public sealed class FeelRuleAdapter<TContext> : IRule<TContext>
{
    private readonly string _code;
    private readonly string _expression;
    private readonly IContextProjector<TContext> _projector;
    private readonly IMLog<FeelRuleAdapter<TContext>> _log;

    public string Code => _code;
    public int Order { get; init; }
    public string[] DependsOn { get; init; } = [];
    public HookPoint HookPoint => HookPoint.BeforeRule;
    public RuleType Type => RuleType.Validation;
    public string Name => $"FEEL:{_code}";
    public IEnumerable<Type> Dependencies => [];

    public FeelRuleAdapter(
        string code,
        string expression,
        IContextProjector<TContext> projector,
        IMLog<FeelRuleAdapter<TContext>> log)
    {
        _code       = code;
        _expression = expression;
        _projector  = projector;
        _log        = log;
    }

    public Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_expression))
        {
            return Task.FromResult(
                RuleResult.Failure($"FEEL expression in '{_code}' is empty."));
        }

        Dictionary<string, object> feelVars = BuildFeelVariables(ctx, facts);

        bool passed;
        try
        {
            passed = FeelEvaluator.Evaluate(_expression, feelVars);
        }
        catch (Exception ex)
        {
            _log.Warn("FEEL expression '{Code}' threw: {Message}", _code, ex.Message);
            return Task.FromResult(
                RuleResult.Failure($"FEEL expression error in '{_code}': {ex.Message}"));
        }

        RuleResult result = passed
            ? RuleResult.Passed()
            : RuleResult.Failure($"Condition '{_code}' evaluated to false");

        return Task.FromResult(result);
    }

    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask; // evaluation-only — no side effects

    private Dictionary<string, object> BuildFeelVariables(TContext ctx, FactBag facts)
    {
        Dictionary<string, object> vars = new(StringComparer.OrdinalIgnoreCase);

        // Layer 1: context fields (lower priority)
        foreach (KeyValuePair<string, object?> kv in _projector.Project(ctx))
        {
            if (kv.Value is not null)
            {
                vars[kv.Key] = kv.Value;
            }
        }

        // Layer 2: current facts (higher priority — upstream rules may override)
        foreach (string key in facts.Keys)
        {
            object? val = facts.Get<object>(key);
            if (val is not null)
            {
                vars[key] = val;
            }
        }

        return vars;
    }
}
