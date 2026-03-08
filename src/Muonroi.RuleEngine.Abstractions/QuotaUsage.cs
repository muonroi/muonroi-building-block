namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Represents the current quota usage and limits for a tenant.
/// </summary>
public sealed class QuotaUsage
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;
    /// <summary>Gets or sets the current usage for each quota type.</summary>
    public Dictionary<QuotaType, int> CurrentUsage { get; set; } = [];
    /// <summary>Gets or sets the limits for each quota type.</summary>
    public Dictionary<QuotaType, int> Limits { get; set; } = [];
    /// <summary>Gets or sets the start of the tracking period.</summary>
    public DateTime PeriodStart { get; set; }
    /// <summary>Gets or sets the end of the tracking period.</summary>
    public DateTime PeriodEnd { get; set; }
}
