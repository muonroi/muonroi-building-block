namespace Muonroi.RuleEngine.Core.Tracing;

/// <summary>
/// Represents a single trace record of a rule execution event.
/// </summary>
public sealed record RuleTraceEntry
{
    /// <summary>
    /// Gets the unique identifier for this trace entry.
    /// </summary>
    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the identifier for the trace session.
    /// </summary>
    public string? TraceSessionId { get; init; }

    /// <summary>
    /// Gets the identifier of the parent node in the execution tree.
    /// </summary>
    public string? ParentNodeId { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant.
    /// </summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the correlation identifier for the execution context.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the user who initiated the execution.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of the source that triggered the rule execution.
    /// </summary>
    public string SourceType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the rule being executed.
    /// </summary>
    public string RuleName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the version of the rule set being executed.
    /// </summary>
    public string RuleSetVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the phase of the rule execution when this trace was captured.
    /// </summary>
    public RuleTracePhase Phase { get; init; }

    /// <summary>
    /// Gets the timestamp when the execution occurred.
    /// </summary>
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the elapsed time in milliseconds for the execution.
    /// </summary>
    public long ElapsedMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the rule execution was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the reason for failure, if the execution failed.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Gets the type of the exception if an error occurred.
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// Gets the message of the exception if an error occurred.
    /// </summary>
    public string? ExceptionMessage { get; init; }

    /// <summary>
    /// Gets the input facts used in the rule execution as a JSON string.
    /// </summary>
    public string? InputFactsJson { get; init; }

    /// <summary>
    /// Gets the output facts resulting from the rule execution as a JSON string.
    /// </summary>
    public string? OutputFactsJson { get; init; }

    /// <summary>
    /// Gets a list of fact keys that were changed during execution.
    /// </summary>
    public IReadOnlyList<string>? ChangedFactKeys { get; init; }
}

/// <summary>
/// Defines the phases of rule execution for tracing purposes.
/// </summary>
public enum RuleTracePhase
{
    /// <summary>
    /// Captured before rule evaluation.
    /// </summary>
    BeforeEval = 0,

    /// <summary>
    /// Captured after rule evaluation.
    /// </summary>
    AfterEval = 1,

    /// <summary>
    /// Captured after rule execution completion.
    /// </summary>
    AfterExec = 2,

    /// <summary>
    /// Captured when an error occurred during execution.
    /// </summary>
    Error = 3,

    /// <summary>
    /// Captured during execution of compensation logic.
    /// </summary>
    Compensate = 4
}
