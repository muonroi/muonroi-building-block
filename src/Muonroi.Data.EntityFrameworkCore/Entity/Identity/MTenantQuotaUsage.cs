namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Tracks quota usage for a tenant within a time bucket.
/// </summary>
[Table("MTenantQuotaUsages")]
public class MTenantQuotaUsage : MEntity
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    [Required]
    [StringLength(128)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the quota type name.</summary>
    [Required]
    [StringLength(64)]
    public string QuotaType { get; set; } = string.Empty;

    /// <summary>Gets or sets the period bucket identifier.</summary>
    [Required]
    [StringLength(32)]
    public string Period { get; set; } = string.Empty;

    /// <summary>Gets or sets the usage amount.</summary>
    public int Amount { get; set; }
    /// <summary>Gets or sets the period start time.</summary>
    public DateTime PeriodStart { get; set; }
    /// <summary>Gets or sets the period end time.</summary>
    public DateTime PeriodEnd { get; set; }
}
