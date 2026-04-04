using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Converters;

/// <summary>
/// Converts a decision table to another representation.
/// </summary>
public interface IDecisionTableConverter
{
    /// <summary>
    /// Converts the provided decision table.
    /// </summary>
    /// <param name="table">Decision table to convert.</param>
    /// <returns>Converted payload.</returns>
    string Convert(DecisionTableModel table);
}
