namespace Muonroi.RuleEngine.Core.Tracing;

public sealed record RuleTraceEntry
{
    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public string RuleSetVersion { get; init; } = string.Empty;
    public RuleTracePhase Phase { get; init; }
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;
    public long ElapsedMs { get; init; }
    public bool IsSuccess { get; init; }
    public string? FailureReason { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
    public string? InputFactsJson { get; init; }
    public string? OutputFactsJson { get; init; }
    public IReadOnlyList<string>? ChangedFactKeys { get; init; }
}

public enum RuleTracePhase
{
    BeforeEval = 0,
    AfterEval = 1,
    AfterExec = 2,
    Error = 3,
    Compensate = 4
}
