namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Audit entry describing a change to a decision table.
/// </summary>
public sealed class DecisionTableAuditEntry
{
    /// <summary>Audit entry identifier.</summary>
    public long Id { get; init; }
    /// <summary>Associated table identifier.</summary>
    public string? TableId { get; init; }
    /// <summary>Action type (create, update, delete, etc.).</summary>
    public string Action { get; init; } = string.Empty;
    /// <summary>Actor who performed the action.</summary>
    public string? Actor { get; init; }
    /// <summary>Optional reason for the action.</summary>
    public string? Reason { get; init; }
    /// <summary>Timestamp of the action.</summary>
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>Optional JSON payload captured for the action.</summary>
    public string? PayloadJson { get; init; }
}
