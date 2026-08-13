namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Represents a rule that can be evaluated against a context object.
/// </summary>
/// <typeparam name="TContext">The type of the context object.</typeparam>
public interface IRule<in TContext>
{
    /// <summary>
    /// Unique code identifying the rule.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Determines the execution order when multiple rules are available at the same level.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Codes of rules that must run before this rule.
    /// </summary>
    IReadOnlyList<string> DependsOn => [];

    /// <summary>
    /// Specifies the hook point where this rule executes.
    /// </summary>
    HookPoint HookPoint => HookPoint.BeforeRule;

    /// <summary>
    /// Evaluates the rule against the given <paramref name="ctx"/>.
    /// </summary>
    Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        return Task.FromResult(RuleResult.Passed());
    }

    /// <summary>
    /// Gets the type of this rule.
    /// </summary>
    RuleType Type => RuleType.Validation;

    /// <summary>
    /// Executes the rule logic using the provided <paramref name="context"/>.
    /// </summary>
    Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>Gets the unique name of the rule.</summary>
    string Name => GetType().Name;

    /// <summary>Gets the rule dependencies that must run before this rule.</summary>
    IEnumerable<Type> Dependencies => [];
}
