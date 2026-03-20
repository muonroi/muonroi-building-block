namespace Muonroi.Governance.License;

/// <summary>
/// Represents the License Action Context.
/// </summary>
public sealed class LicenseActionContext
{
    /// <summary>
    /// Gets or sets the Action Name.
    /// </summary>
    public string? ActionName { get; set; }
    /// <summary>
    /// Gets or sets the Action Type.
    /// </summary>
    public string? ActionType { get; set; }
    /// <summary>
    /// Gets or sets the Payload Hash.
    /// </summary>
    public string? PayloadHash { get; set; }
    /// <summary>
    /// Gets or sets the Correlation Id.
    /// </summary>
    public string? CorrelationId { get; set; }
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
