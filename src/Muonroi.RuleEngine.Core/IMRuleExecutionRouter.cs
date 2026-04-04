namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Routes execution to traditional code, rule engine, or both based on runtime mode.
/// </summary>
public interface IMRuleExecutionRouter<TContext>
{
    /// <summary>
    /// Executes the rule or the traditional logic path based on the execution mode.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="traditionalPath">The traditional logic path.</param>
    /// <param name="modeOverride">Optional mode override.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{FactBag}"/> representing the asynchronous operation.</returns>
    Task<FactBag> ExecuteAsync(
        TContext context,
        Func<CancellationToken, Task>? traditionalPath = null,
        RuleExecutionMode? modeOverride = null,
        CancellationToken cancellationToken = default);
}
