using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Represents metadata about a rule that can be exposed to UI.
/// </summary>
/// <param name="Code">Unique rule code used for identification.</param>
/// <param name="Name">Human readable name of the rule.</param>
/// <param name="Description">Short description of what the rule does.</param>
/// <param name="HookPoint">Execution hook point of the rule.</param>
/// <param name="Order">Explicit order of the rule. Lower values run first.</param>
/// <param name="DependsOn">Codes of rules that must run before this rule.</param>
/// <param name="DefaultEnabled">Whether the rule is enabled by default.</param>
public sealed record RuleDescriptor(
    string Code,
    string Name,
    string Description,
    RuleType HookPoint,
    int Order = 0,
    string[]? DependsOn = null,
    bool DefaultEnabled = true)
{
    /// <summary>List of codes this rule depends on.</summary>
    public string[] DependsOn { get; init; } = DependsOn ?? [];
}
