namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Provides context for rule execution.
/// </summary>
public interface IRuleContext
{
    /// <summary>
    /// Stops execution of remaining rules in the current group.
    /// </summary>
    void HaltGroup();
}
