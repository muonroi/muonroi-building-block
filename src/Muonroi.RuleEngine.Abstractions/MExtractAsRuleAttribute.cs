namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Marks a method as a code-first rule candidate for extraction tools.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class MExtractAsRuleAttribute(string code) : Attribute
{
    public string Code { get; } = code;
    public int Order { get; set; }
    public HookPoint HookPoint { get; set; } = HookPoint.BeforeRule;
    public string[] DependsOn { get; set; } = [];
}
