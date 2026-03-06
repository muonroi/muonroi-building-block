namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MTenantQuotaUsages")]
public class MTenantQuotaUsage : MEntity
{
    [Required]
    [StringLength(128)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string QuotaType { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string Period { get; set; } = string.Empty;

    public int Amount { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}
