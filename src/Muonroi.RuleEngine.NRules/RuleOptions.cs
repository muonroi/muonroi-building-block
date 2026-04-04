


namespace Muonroi.RuleEngine.NRules;

/// <summary>Configuration for rule enabling and versioning.</summary>
[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed class RuleOptions
{
    /// <summary>Configuration per rule name.</summary>
    public Dictionary<string, RuleConfig> Rules { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Configuration for a specific rule.</summary>
[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed class RuleConfig
{
    /// <summary>Whether the rule is enabled. Defaults to <c>true</c>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Specific version of the rule to use. If null, all versions are enabled.</summary>
    public string? Version { get; set; }
}
