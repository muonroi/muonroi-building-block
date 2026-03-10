namespace Muonroi.Core.Abstractions.Diagnostics;

/// <summary>
/// Defines the types of nodes in a trace tree.
/// </summary>
public enum MTraceNodeType
{
    MediatorRequest,
    PipelineBehavior,
    RuleSet,
    Rule,
    Handler,
    Custom
}

/// <summary>
/// Represents an active diagnostic trace session.
/// </summary>
public interface ITraceSession
{
    string SessionId { get; }
    string? TenantId { get; }
    string? UserId { get; }
    bool IsActive { get; }
    bool IsLineTraceEnabled { get; }

    /// <summary>Opens a new child node scope under the current context node.</summary>
    IDisposable BeginNode(string name, MTraceNodeType type);

    /// <summary>Records a structured log event on current node.</summary>
    void Record(string message, object? payload = null);

    /// <summary>Records a fact snapshot (Level 1 only).</summary>
    void RecordFactSnapshot(string phase, IReadOnlyDictionary<string, object?> facts);

    /// <summary>Records line-level capture (Level 2 only, gated by IsLineTraceEnabled).</summary>
    void RecordLineTrace(int line, string variable, object? value, string? sourceMember = null);

    /// <summary>Records a branch taken (Level 2 only).</summary>
    void RecordBranchTrace(int line, string condition, bool taken);

    /// <summary>Marks current node as failed.</summary>
    void MarkFailed(string reason, Exception? ex = null);

    /// <summary>Exports the complete trace session data.</summary>
    MTraceSessionRecord Export();
}
