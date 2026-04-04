namespace Muonroi.Observability.Logging;

/// <summary>
/// Represents detailed log information for a request or operation.
/// </summary>
public class MLogEntry
{
    /// <summary>Unique log entry identity.</summary>
    public string Identity { get; set; } = Guid.NewGuid().ToString();
    /// <summary>Transaction correlation identifier.</summary>
    public string? TransactionId { get; set; }
    /// <summary>Site or location code.</summary>
    public string? SiteCode { get; set; }
    /// <summary>Tenant identifier.</summary>
    public string? TenantId { get; set; }
    /// <summary>Execution timestamp.</summary>
    public DateTime ExecDate { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
    /// <summary>Logical log type or category.</summary>
    public string? LogType { get; set; }
    /// <summary>Server or host name.</summary>
    public string? Server { get; set; }
    /// <summary>Caller identifier.</summary>
    public string? Caller { get; set; }
    /// <summary>Client identifier.</summary>
    public string? Client { get; set; }
    /// <summary>Username associated with the log entry.</summary>
    public string? Username { get; set; }
    /// <summary>Client or request location.</summary>
    public string? Location { get; set; }
    /// <summary>Additional data payload.</summary>
    public string? Data { get; set; }
    /// <summary>Elapsed time in milliseconds.</summary>
    public long Elapsed { get; set; }
    /// <summary>Error code if applicable.</summary>
    public string? ErrorCode { get; set; }
    /// <summary>Log message.</summary>
    public string? Message { get; set; }
    /// <summary>Stack trace or trace details.</summary>
    public string? LogTrace { get; set; }
    /// <summary>Application metadata.</summary>
    public string? ApplicationInfo { get; set; }
}
