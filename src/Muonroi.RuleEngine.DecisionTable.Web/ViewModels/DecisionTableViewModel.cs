namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

/// <summary>
/// View model for decision table details and row counts.
/// </summary>
public sealed class DecisionTableViewModel
{
    /// <summary>
    /// The decision table.
    /// </summary>
    public required DecisionTableModel Table { get; init; }
    /// <summary>
    /// Count of enabled rows.
    /// </summary>
    public int EnabledRowCount => Table.Rows.Count(x => x.IsEnabled);
    /// <summary>
    /// Count of disabled rows.
    /// </summary>
    public int DisabledRowCount => Table.Rows.Count(x => !x.IsEnabled);
}
