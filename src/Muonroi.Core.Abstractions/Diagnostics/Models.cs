namespace Muonroi.Core.Abstractions.Diagnostics;

/// <summary>
/// Snapshot of a trace session for export.
/// </summary>
public sealed class MTraceSessionRecord
{
    /// <summary>Trace session identifier.</summary>
    public string SessionId { get; init; } = string.Empty;
    /// <summary>Tenant identifier, if available.</summary>
    public string? TenantId { get; init; }
    /// <summary>User identifier, if available.</summary>
    public string? UserId { get; init; }
    /// <summary>Session start timestamp.</summary>
    public DateTime StartedAt { get; init; }
    /// <summary>Total duration in milliseconds.</summary>
    public double DurationMs { get; init; }
    /// <summary>Whether the session contains errors.</summary>
    public bool HasErrors { get; init; }
    /// <summary>Root trace nodes.</summary>
    public IReadOnlyList<MTraceNodeRecord> Nodes { get; init; } = [];
}

/// <summary>
/// Snapshot of a trace node within a session.
/// </summary>
public sealed class MTraceNodeRecord
{
    /// <summary>Node identifier.</summary>
    public string NodeId { get; init; } = string.Empty;
    /// <summary>Node display name.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Type of node.</summary>
    public MTraceNodeType Type { get; init; }
    /// <summary>Node start timestamp.</summary>
    public DateTime StartedAt { get; init; }
    /// <summary>Node duration in milliseconds.</summary>
    public double DurationMs { get; set; }
    /// <summary>Whether the node captured an error.</summary>
    public bool HasError { get; set; }
    /// <summary>Error reason text, if any.</summary>
    public string? ErrorReason { get; set; }
    /// <summary>Serialized request payload.</summary>
    public string? RequestJson { get; set; }
    /// <summary>Serialized response payload.</summary>
    public string? ResponseJson { get; set; }
    /// <summary>Serialized input facts.</summary>
    public string? InputFactsJson { get; set; }
    /// <summary>Serialized output facts.</summary>
    public string? OutputFactsJson { get; set; }
    /// <summary>Keys of facts that changed.</summary>
    public IReadOnlyList<string> ChangedFactKeys { get; set; } = [];
    /// <summary>Line-level traces captured under this node.</summary>
    public List<MLineTraceRecord> LineTraces { get; } = [];
    /// <summary>Child nodes.</summary>
    public List<MTraceNodeRecord> Children { get; } = [];
    /// <summary>Events recorded under this node.</summary>
    public List<MTraceEventRecord> Events { get; } = [];
}

/// <summary>
/// Snapshot of a line-level trace.
/// </summary>
public sealed class MLineTraceRecord
{
    /// <summary>Line number.</summary>
    public int Line { get; init; }
    /// <summary>Variable or symbol name.</summary>
    public string Variable { get; init; } = string.Empty;
    /// <summary>Serialized value.</summary>
    public string? ValueJson { get; init; }
    /// <summary>Whether the line represents a branch.</summary>
    public bool IsBranch { get; init; }
    /// <summary>Branch condition text.</summary>
    public string? Condition { get; init; }
    /// <summary>Whether the branch was taken.</summary>
    public bool? BranchTaken { get; init; }
}

/// <summary>
/// Snapshot of a trace event.
/// </summary>
public sealed class MTraceEventRecord
{
    /// <summary>Event message.</summary>
    public string Message { get; init; } = string.Empty;
    /// <summary>Serialized payload, if any.</summary>
    public string? PayloadJson { get; init; }
    /// <summary>Event timestamp.</summary>
    public DateTime Timestamp { get; init; }
}
