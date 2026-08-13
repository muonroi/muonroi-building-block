namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Attribute that associates a rule with a workflow and tenant key for multi-tenant registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TenantRuleGroupAttribute(string workflow, string tenant) : Attribute
{

    /// <summary>The workflow key that the rule belongs to.</summary>
    public string Workflow { get; } = workflow;

    /// <summary>The tenant that the rule is scoped for.</summary>
    public string Tenant { get; } = tenant;

    /// <summary>The combined registration key.</summary>
    public string Key { get; } = $"{workflow}:{tenant}";
}
