namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Strongly typed options controlling the enabled state of rules.
/// </summary>
public sealed class RuleOptions
{
    /// <summary>
    /// Gets or sets the map of rule codes to an enabled flag.
    /// When a rule code is present and set to <c>false</c>, the rule
    /// will be skipped by <see cref="RuleEngine{T}"/>. Rules not present
    /// in this dictionary are considered enabled.
    /// </summary>
    public Dictionary<string, bool> RuleToggles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional map of tenant identifiers to rule toggle dictionaries.
    /// When present, entries under the current tenant will override <see cref="RuleToggles"/>
    /// for that tenant only, allowing per-tenant feature flagging.
    /// </summary>
    public Dictionary<string, Dictionary<string, bool>> TenantRuleToggles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
