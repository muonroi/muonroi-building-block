

namespace Muonroi.RuleEngine.NRules;

/// <summary>
/// Metadata describing a rule's identity and version.
/// </summary>
[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RuleAttribute(string name, string version) : Attribute
{
    /// <summary>Name of the rule.</summary>
    public string Name { get; } = name;

    /// <summary>Version of the rule.</summary>
    public string Version { get; } = version;
}
