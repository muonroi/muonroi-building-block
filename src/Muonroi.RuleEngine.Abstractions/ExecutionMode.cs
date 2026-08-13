namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Defines how a rule orchestrator handles failures and compensation.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Stops on the first failure and does not attempt compensation.
    /// This is the default for backward compatibility.
    /// </summary>
    AllOrNothing = 0,

    /// <summary>
    /// Continues executing remaining rules after failures and aggregates the errors.
    /// </summary>
    BestEffort = 1,

    /// <summary>
    /// Stops on the first failure and compensates previously executed rules
    /// that implement ICompensatableRule&lt;TContext&gt; in reverse order.
    /// </summary>
    CompensateOnFailure = 2
}
