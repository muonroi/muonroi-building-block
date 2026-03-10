namespace Muonroi.RuleEngine.Abstractions.Authoring;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MRuleFactDescriptionAttribute(string factKey) : Attribute
{
    public string FactKey { get; } = factKey;
    public string? Label { get; set; }
    public string? Description { get; set; }
    public string? Example { get; set; }
}
