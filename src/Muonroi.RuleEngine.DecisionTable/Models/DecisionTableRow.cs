namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTableRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Order { get; set; }
    public string? Description { get; set; }
    public List<DecisionTableCell> InputCells { get; set; } = [];
    public List<DecisionTableCell> OutputCells { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
}
