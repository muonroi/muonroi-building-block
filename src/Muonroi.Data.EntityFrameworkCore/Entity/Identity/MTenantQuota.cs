using Muonroi.Quota.Abstractions;

namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Stores quota limits for a tenant.
/// </summary>
[Table("MTenantQuotas")]
public class MTenantQuota : MEntity
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    [Required]
    [StringLength(128)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant tier.</summary>
    public TenantTier Tier { get; set; } = TenantTier.Free;
    /// <summary>Gets or sets the maximum number of rules per tenant.</summary>
    public int MaxRulesPerTenant { get; set; }
    /// <summary>Gets or sets the maximum rule executions per day.</summary>
    public int MaxRuleExecutionsPerDay { get; set; }
    /// <summary>Gets or sets the maximum concurrent executions.</summary>
    public int MaxConcurrentExecutions { get; set; }
    /// <summary>Gets or sets the maximum number of decision tables.</summary>
    public int MaxDecisionTables { get; set; }
    /// <summary>Gets or sets the maximum number of JSON workflows.</summary>
    public int MaxJsonWorkflows { get; set; }
    /// <summary>Gets or sets the maximum storage size in MB.</summary>
    public int MaxStorageMB { get; set; }
    /// <summary>Gets or sets the maximum API requests per minute.</summary>
    public int MaxApiRequestsPerMinute { get; set; }
    /// <summary>Gets or sets the maximum rule evaluations per second.</summary>
    public int MaxRuleEvaluationsPerSecond { get; set; }
    /// <summary>Gets or sets the maximum workflow executions per hour.</summary>
    public int MaxWorkflowExecutionsPerHour { get; set; }
    /// <summary>Gets or sets the maximum rule complexity.</summary>
    public int MaxRuleComplexity { get; set; }
    /// <summary>Gets or sets the maximum workflow size in KB.</summary>
    public int MaxWorkflowSizeKB { get; set; }
    /// <summary>Gets or sets the maximum execution time in milliseconds.</summary>
    public int MaxExecutionTimeMs { get; set; }
    /// <summary>Gets or sets the maximum total connectors.</summary>
    public int MaxTotalConnectors { get; set; }
    /// <summary>Gets or sets the maximum connector executions per day.</summary>
    public int MaxConnectorExecutionsPerDay { get; set; }
}
