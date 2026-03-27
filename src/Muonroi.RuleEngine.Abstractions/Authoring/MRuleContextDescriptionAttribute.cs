namespace Muonroi.RuleEngine.Abstractions.Authoring;

/// <summary>
/// Describes a rule context for authoring metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MRuleContextDescriptionAttribute : Attribute
{
    /// <summary>Display title for the context.</summary>
    public string? Title { get; set; }
    /// <summary>Human-readable description of the context.</summary>
    public string? Description { get; set; }
}
