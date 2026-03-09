using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable;

public interface IDecisionTableExecutor
{
    Task<DecisionTableExecutionResult> ExecuteAsync(
        DecisionTableModel table,
        IReadOnlyDictionary<string, object?> inputFacts,
        CancellationToken cancellationToken = default);
}
