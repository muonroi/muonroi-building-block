namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

public sealed class DecisionTableViewModel
{
    public required DecisionTableModel Table { get; init; }
    public int EnabledRowCount => Table.Rows.Count(x => x.IsEnabled);
    public int DisabledRowCount => Table.Rows.Count(x => !x.IsEnabled);
}
