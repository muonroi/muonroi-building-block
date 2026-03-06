using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Converters;

public interface IDecisionTableConverter
{
    string Convert(DecisionTableModel table);
}
