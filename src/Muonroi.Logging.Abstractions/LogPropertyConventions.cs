namespace Muonroi.Logging.Abstractions;

/// <summary>
/// Defines standard property keys for structured logging in Muonroi.
/// </summary>
public static class LogPropertyConventions
{
    public const string TenantId = "TenantId";
    public const string UserId = "UserId";
    public const string CorrelationId = "CorrelationId";
    public const string TraceSessionId = "TraceSessionId";
    public const string RuleCode = "RuleCode";
    public const string RequestName = "RequestName";
}
