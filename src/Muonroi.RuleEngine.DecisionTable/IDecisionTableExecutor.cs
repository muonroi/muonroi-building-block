using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable;

/// <summary>
/// Executes decision tables against input facts.
/// </summary>
public interface IDecisionTableExecutor
{
    /// <summary>
    /// Evaluates the specified table against the provided inputs.
    /// </summary>
    /// <param name="table">Decision table to execute.</param>
    /// <param name="inputFacts">Input facts keyed by column name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result containing matches and outputs.</returns>
    Task<DecisionTableExecutionResult> ExecuteAsync(
        DecisionTableModel table,
        IReadOnlyDictionary<string, object?> inputFacts,
        CancellationToken cancellationToken = default);
}
