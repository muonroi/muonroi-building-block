namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTableCell
{
    public string ColumnId { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string? Comment { get; set; }
}
