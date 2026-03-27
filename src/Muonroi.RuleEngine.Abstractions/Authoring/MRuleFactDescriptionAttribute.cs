namespace Muonroi.RuleEngine.Abstractions.Authoring;

/// <summary>
/// Describes a fact used by a rule for authoring metadata.
/// </summary>
/// <param name="factKey">Unique key for the fact.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MRuleFactDescriptionAttribute(string factKey) : Attribute
{
    /// <summary>Unique key for the fact.</summary>
    public string FactKey { get; } = factKey;
    /// <summary>Display label for the fact.</summary>
    public string? Label { get; set; }
    /// <summary>Description of the fact.</summary>
    public string? Description { get; set; }
    /// <summary>Example value or usage.</summary>
    public string? Example { get; set; }
}
