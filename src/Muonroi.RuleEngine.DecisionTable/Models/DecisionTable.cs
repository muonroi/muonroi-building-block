namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Represents a DMN-style decision table.
/// </summary>
public sealed class DecisionTable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public HitPolicy HitPolicy { get; set; } = HitPolicy.First;
    public List<DecisionTableColumn> InputColumns { get; set; } = [];
    public List<DecisionTableColumn> OutputColumns { get; set; } = [];
    public List<DecisionTableRow> Rows { get; set; } = [];
    public int Version { get; set; } = 1;
    public string? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}
