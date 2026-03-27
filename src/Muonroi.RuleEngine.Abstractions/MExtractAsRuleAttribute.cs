namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Marks a method as a code-first rule candidate for extraction tools.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class MExtractAsRuleAttribute(string code) : Attribute
{
    /// <summary>
    /// Gets the unique code identifying the rule.
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// Gets or sets the execution order of the extracted rule.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the hook point where the rule should be executed.
    /// </summary>
    public HookPoint HookPoint { get; set; } = HookPoint.BeforeRule;

    /// <summary>
    /// Gets or sets the list of rule codes that must be executed before this rule.
    /// </summary>
    public string[] DependsOn { get; set; } = [];

    /// <summary>
    /// Gets or sets an inline FEEL expression used to generate the rule body.
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// Gets or sets whether the generated rule should inherit from
    /// <see cref="MFactBagAwareRule{TContext}"/> instead of implementing
    /// <see cref="IRule{TContext}"/> directly. When <c>true</c>, the generated rule
    /// uses <c>ReadFact</c>/<c>WriteFact</c> helpers and overrides <c>EvaluateCoreAsync</c>.
    /// </summary>
    public bool UseFactBagAware { get; set; }
}
