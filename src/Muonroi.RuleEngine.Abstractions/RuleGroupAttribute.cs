namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Attribute used to associate a rule or hook handler with a registration key.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RuleGroupAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
