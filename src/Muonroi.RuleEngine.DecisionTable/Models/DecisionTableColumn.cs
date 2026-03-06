namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTableColumn
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; } = true;
}
