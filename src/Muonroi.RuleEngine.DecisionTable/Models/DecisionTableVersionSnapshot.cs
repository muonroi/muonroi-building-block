namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Snapshot of a decision table at a specific version.
/// </summary>
public sealed class DecisionTableVersionSnapshot
{
    /// <summary>Identifier of the table.</summary>
    public string TableId { get; init; } = string.Empty;
    /// <summary>Version number of the snapshot.</summary>
    public int Version { get; init; }
    /// <summary>Change type that produced this snapshot.</summary>
    public string ChangeType { get; init; } = string.Empty;
    /// <summary>Actor who performed the change.</summary>
    public string? Actor { get; init; }
    /// <summary>Reason for the change.</summary>
    public string? Reason { get; init; }
    /// <summary>Timestamp of the change.</summary>
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>Decision table content for this version.</summary>
    public required DecisionTableModel Table { get; init; }
}
