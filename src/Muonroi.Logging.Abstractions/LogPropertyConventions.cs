namespace Muonroi.Logging.Abstractions;

/// <summary>
/// Defines standard property keys for structured logging in Muonroi.
/// </summary>
public static class LogPropertyConventions
{
    /// <summary>Tenant identifier property key.</summary>
    public const string TenantId = "tenant.id";
    /// <summary>User identifier property key.</summary>
    public const string UserId = "user.id";
    /// <summary>Correlation identifier property key.</summary>
    public const string CorrelationId = "trace.correlation_id";
    /// <summary>Trace session identifier property key.</summary>
    public const string TraceSessionId = "trace.session_id";
    /// <summary>Rule code property key.</summary>
    public const string RuleCode = "muonroi.rule.code";
    /// <summary>Request name property key.</summary>
    public const string RequestName = "http.request.name";
    
    /// <summary>Event kind property key (e.g., audit, event, alert, metric, state).</summary>
    public const string EventKind = "event.kind";
    
    /// <summary>Event category property key (e.g., authentication, database, driver, file, network, process, web).</summary>
    public const string EventCategory = "event.category";
}
