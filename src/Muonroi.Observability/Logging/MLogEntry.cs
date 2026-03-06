namespace Muonroi.Observability.Logging;

/// <summary>
/// Represents detailed log information for a request or operation.
/// </summary>
public class MLogEntry
{
    public string Identity { get; set; } = Guid.NewGuid().ToString();
    public string? TransactionId { get; set; }
    public string? SiteCode { get; set; }
    public string? TenantId { get; set; }
    public DateTime ExecDate { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
    public string? LogType { get; set; }
    public string? Server { get; set; }
    public string? Caller { get; set; }
    public string? Client { get; set; }
    public string? Username { get; set; }
    public string? Location { get; set; }
    public string? Data { get; set; }
    public long Elapsed { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public string? LogTrace { get; set; }
    public string? ApplicationInfo { get; set; }
}
