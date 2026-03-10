namespace Muonroi.Core.Abstractions.Diagnostics;

public sealed class MTraceSessionRecord
{
    public string SessionId { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public string? UserId { get; init; }
    public DateTime StartedAt { get; init; }
    public double DurationMs { get; init; }
    public bool HasErrors { get; init; }
    public IReadOnlyList<MTraceNodeRecord> Nodes { get; init; } = [];
}

public sealed class MTraceNodeRecord
{
    public string NodeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MTraceNodeType Type { get; init; }
    public DateTime StartedAt { get; init; }
    public double DurationMs { get; set; }
    public bool HasError { get; set; }
    public string? ErrorReason { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? InputFactsJson { get; set; }
    public string? OutputFactsJson { get; set; }
    public IReadOnlyList<string> ChangedFactKeys { get; set; } = [];
    public List<MLineTraceRecord> LineTraces { get; } = [];
    public List<MTraceNodeRecord> Children { get; } = [];
    public List<MTraceEventRecord> Events { get; } = [];
}

public sealed class MLineTraceRecord
{
    public int Line { get; init; }
    public string Variable { get; init; } = string.Empty;
    public string? ValueJson { get; init; }
    public bool IsBranch { get; init; }
    public string? Condition { get; init; }
    public bool? BranchTaken { get; init; }
}

public sealed class MTraceEventRecord
{
    public string Message { get; init; } = string.Empty;
    public string? PayloadJson { get; init; }
    public DateTime Timestamp { get; init; }
}
