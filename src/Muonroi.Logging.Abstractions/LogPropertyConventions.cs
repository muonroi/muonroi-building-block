namespace Muonroi.Logging.Abstractions;

/// <summary>
/// Defines standard property keys for structured logging in Muonroi.
/// </summary>
public static class LogPropertyConventions
{
    /// <summary>Tenant identifier property key.</summary>
    public const string TenantId = "TenantId";
    /// <summary>User identifier property key.</summary>
    public const string UserId = "UserId";
    /// <summary>Correlation identifier property key.</summary>
    public const string CorrelationId = "CorrelationId";
    /// <summary>Trace session identifier property key.</summary>
    public const string TraceSessionId = "TraceSessionId";
    /// <summary>Rule code property key.</summary>
    public const string RuleCode = "RuleCode";
    /// <summary>Request name property key.</summary>
    public const string RequestName = "RequestName";
}
