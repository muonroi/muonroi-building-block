using Muonroi.Tenancy.Abstractions.Models;

namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MTenantQuotas")]
public class MTenantQuota : MEntity
{
    [Required]
    [StringLength(128)]
    public string TenantId { get; set; } = string.Empty;

    public TenantTier Tier { get; set; } = TenantTier.Free;
    public int MaxRulesPerTenant { get; set; }
    public int MaxRuleExecutionsPerDay { get; set; }
    public int MaxConcurrentExecutions { get; set; }
    public int MaxDecisionTables { get; set; }
    public int MaxJsonWorkflows { get; set; }
    public int MaxStorageMB { get; set; }
    public int MaxApiRequestsPerMinute { get; set; }
    public int MaxRuleEvaluationsPerSecond { get; set; }
    public int MaxWorkflowExecutionsPerHour { get; set; }
    public int MaxRuleComplexity { get; set; }
    public int MaxWorkflowSizeKB { get; set; }
    public int MaxExecutionTimeMs { get; set; }
}
