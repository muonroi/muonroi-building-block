namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Routes execution to traditional code, rule engine, or both based on runtime mode.
/// </summary>
public interface IMRuleExecutionRouter<TContext>
{
    Task<FactBag> ExecuteAsync(
        TContext context,
        Func<CancellationToken, Task>? traditionalPath = null,
        RuleExecutionMode? modeOverride = null,
        CancellationToken cancellationToken = default);
}
