namespace Muonroi.AspNetCore.Logging;

/// <summary>
/// Represents detailed log information for a request or operation.
/// </summary>
public class MLogEntry
{
    /// <summary>
    /// Gets or sets the unique identity of the log entry.
    /// </summary>
    public string Identity { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the transaction identifier associated with the log entry.
    /// </summary>
    public string? TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the site code associated with the log entry.
    /// </summary>
    public string? SiteCode { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier associated with the log entry.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the execution date and time of the log entry.
    /// </summary>
    public DateTime ExecDate { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary

    /// <summary>
    /// Gets or sets the type of log.
    /// </summary>
    public string? LogType { get; set; }

    /// <summary>
    /// Gets or sets the server where the log was generated.
    /// </summary>
    public string? Server { get; set; }

    /// <summary>
    /// Gets or sets the caller information.
    /// </summary>
    public string? Caller { get; set; }

    /// <summary>
    /// Gets or sets the client information.
    /// </summary>
    public string? Client { get; set; }

    /// <summary>
    /// Gets or sets the username associated with the log entry.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the location of the log generation.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the data associated with the log entry.
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets the elapsed time for the operation in milliseconds.
    /// </summary>
    public long Elapsed { get; set; }

    /// <summary>
    /// Gets or sets the error code, if any.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the log message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the log trace information.
    /// </summary>
    public string? LogTrace { get; set; }

    /// <summary>
    /// Gets or sets the application information.
    /// </summary>
    public string? ApplicationInfo { get; set; }
}
