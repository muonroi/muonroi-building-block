namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Represents a DMN-style decision table.
/// </summary>
public sealed class DecisionTable
{
    /// <summary>Unique identifier for the table.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Table name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Human-readable description.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Hit policy used to select matched rows.</summary>
    public HitPolicy HitPolicy { get; set; } = HitPolicy.First;
    /// <summary>Input column definitions.</summary>
    public List<DecisionTableColumn> InputColumns { get; set; } = [];
    /// <summary>Output column definitions.</summary>
    public List<DecisionTableColumn> OutputColumns { get; set; } = [];
    /// <summary>Decision table rows.</summary>
    public List<DecisionTableRow> Rows { get; set; } = [];
    /// <summary>Version number.</summary>
    public int Version { get; set; } = 1;
    /// <summary>Optional tenant identifier.</summary>
    public string? TenantId { get; set; }
    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Last modification timestamp in UTC.</summary>
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}
