namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Declares the preferred rule execution mode for an API endpoint or class.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class MRuleModeAttribute(RuleExecutionMode mode) : Attribute
{
    public RuleExecutionMode Mode { get; } = mode;
}
