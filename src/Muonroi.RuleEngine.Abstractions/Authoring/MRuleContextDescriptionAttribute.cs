namespace Muonroi.RuleEngine.Abstractions.Authoring;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MRuleContextDescriptionAttribute : Attribute
{
    public string? Title { get; set; }
    public string? Description { get; set; }
}
