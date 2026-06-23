namespace Muonroi.Quota.Abstractions;

/// <summary>
/// Represents the quota limits for a tenant.
/// </summary>
public sealed class TenantQuota
{
    /// <summary>
    /// Gets or sets the unique identifier of the tenant.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of rules allowed per tenant.
    /// </summary>
    public int MaxRulesPerTenant { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of rule executions allowed per day.
    /// </summary>
    public int MaxRuleExecutionsPerDay { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum number of concurrent executions allowed.
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of decision tables allowed.
    /// </summary>
    public int MaxDecisionTables { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of JSON workflows allowed.
    /// </summary>
    public int MaxJsonWorkflows { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum storage allowed in megabytes (MB).
    /// </summary>
    public int MaxStorageMB { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of API requests allowed per minute.
    /// </summary>
    public int MaxApiRequestsPerMinute { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of rule evaluations allowed per second.
    /// </summary>
    public int MaxRuleEvaluationsPerSecond { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of workflow executions allowed per hour.
    /// </summary>
    public int MaxWorkflowExecutionsPerHour { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum complexity allowed for a single rule.
    /// </summary>
    public int MaxRuleComplexity { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum size allowed for a workflow in kilobytes (KB).
    /// </summary>
    public int MaxWorkflowSizeKB { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum execution time allowed for a rule/workflow in milliseconds.
    /// </summary>
    public int MaxExecutionTimeMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the maximum number of messages allowed per day.
    /// </summary>
    public int MaxMessagesPerDay { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum number of messages allowed per minute.
    /// </summary>
    public int MaxMessagesPerMinute { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum number of connectors allowed.
    /// </summary>
    public int MaxTotalConnectors { get; set; } = 2;

    /// <summary>
    /// Gets or sets the maximum number of connector executions per day.
    /// </summary>
    public int MaxConnectorExecutionsPerDay { get; set; } = 100;

    /// <summary>
    /// Gets or sets the enforced per-tier maximum number of PDF render events allowed per day
    /// (Phase 17 / MON-04). Sourced from the licensed tier: finite for non-Enterprise tiers
    /// (Free/Starter/Professional) and <see cref="int.MaxValue"/> (unlimited) for Enterprise.
    /// The instance default is <see cref="int.MaxValue"/>; tier presets set finite caps.
    /// </summary>
    public int MaxPdfRendersPerDay { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the tenant tier.
    /// </summary>
    public TenantTier Tier { get; set; } = TenantTier.Free;
}

/// <summary>
/// Specifies the tier of a tenant.
/// </summary>
public enum TenantTier
{
    /// <summary>
    /// Free tier.
    /// </summary>
    Free,

    /// <summary>
    /// Starter tier.
    /// </summary>
    Starter,

    /// <summary>
    /// Professional tier.
    /// </summary>
    Professional,

    /// <summary>
    /// Enterprise tier.
    /// </summary>
    Enterprise
}

/// <summary>
/// Provides predefined quota presets for different tenant tiers.
/// </summary>
public static class TenantQuotaPresets
{
    /// <summary>
    /// Gets the quota preset for the Free tier.
    /// </summary>
    public static TenantQuota Free
    {
        get
        {
            TenantQuota free = new()
            {
                Tier = TenantTier.Free,
                MaxRulesPerTenant = 10,
                MaxRuleExecutionsPerDay = 1000,
                MaxConcurrentExecutions = 2,
                MaxDecisionTables = 5,
                MaxJsonWorkflows = 10,
                MaxStorageMB = 10,
                MaxApiRequestsPerMinute = 20,
                MaxRuleEvaluationsPerSecond = 10,
                MaxWorkflowExecutionsPerHour = 100,
                MaxRuleComplexity = 5,
                MaxWorkflowSizeKB = 50,
                MaxExecutionTimeMs = 1000,
                MaxMessagesPerDay = 1000,
                MaxMessagesPerMinute = 50,
                MaxTotalConnectors = 2,
                MaxConnectorExecutionsPerDay = 100,
                // MON-04: finite per-tier PDF render cap (Free = smallest daily allowance).
                MaxPdfRendersPerDay = 50
            };
            return free;
        }
    }

    /// <summary>
    /// Gets the quota preset for the Starter tier.
    /// </summary>
    public static TenantQuota Starter
    {
        get
        {
            TenantQuota quota = new()
            {
                Tier = TenantTier.Starter,
                MaxRulesPerTenant = 50,
                MaxRuleExecutionsPerDay = 10_000,
                MaxConcurrentExecutions = 5,
                MaxDecisionTables = 20,
                MaxJsonWorkflows = 50,
                MaxStorageMB = 50,
                MaxApiRequestsPerMinute = 100,
                MaxRuleEvaluationsPerSecond = 50,
                MaxWorkflowExecutionsPerHour = 1000,
                MaxRuleComplexity = 10,
                MaxWorkflowSizeKB = 200,
                MaxExecutionTimeMs = 3000,
                MaxMessagesPerDay = 10_000,
                MaxMessagesPerMinute = 200,
                MaxTotalConnectors = 10,
                MaxConnectorExecutionsPerDay = 1000,
                // MON-04: finite per-tier PDF render cap (Starter > Free).
                MaxPdfRendersPerDay = 500
            };
            return quota;
        }
    }

    /// <summary>
    /// Gets the quota preset for the Professional tier.
    /// </summary>
    public static TenantQuota Professional
    {
        get
        {
            TenantQuota quota = new()
            {
                Tier = TenantTier.Professional,
                MaxRulesPerTenant = 200,
                MaxRuleExecutionsPerDay = 100_000,
                MaxConcurrentExecutions = 20,
                MaxDecisionTables = 100,
                MaxJsonWorkflows = 200,
                MaxStorageMB = 500,
                MaxApiRequestsPerMinute = 500,
                MaxRuleEvaluationsPerSecond = 200,
                MaxWorkflowExecutionsPerHour = 10_000,
                MaxRuleComplexity = 20,
                MaxWorkflowSizeKB = 1000,
                MaxExecutionTimeMs = 10_000,
                MaxMessagesPerDay = 100_000,
                MaxMessagesPerMinute = 1000,
                MaxTotalConnectors = 50,
                MaxConnectorExecutionsPerDay = 10_000,
                // MON-04: finite per-tier PDF render cap (Professional > Starter).
                MaxPdfRendersPerDay = 5_000
            };
            return quota;
        }
    }

    /// <summary>
    /// Gets the quota preset for the Enterprise tier.
    /// </summary>
    public static TenantQuota Enterprise
    {
        get
        {
            TenantQuota quota = new()
            {
                Tier = TenantTier.Enterprise,
                MaxRulesPerTenant = int.MaxValue,
                MaxRuleExecutionsPerDay = int.MaxValue,
                MaxConcurrentExecutions = 100,
                MaxDecisionTables = int.MaxValue,
                MaxJsonWorkflows = int.MaxValue,
                MaxStorageMB = int.MaxValue,
                MaxApiRequestsPerMinute = int.MaxValue,
                MaxRuleEvaluationsPerSecond = int.MaxValue,
                MaxWorkflowExecutionsPerHour = int.MaxValue,
                MaxRuleComplexity = int.MaxValue,
                MaxWorkflowSizeKB = int.MaxValue,
                MaxExecutionTimeMs = 60_000,
                MaxMessagesPerDay = int.MaxValue,
                MaxMessagesPerMinute = int.MaxValue,
                MaxTotalConnectors = int.MaxValue,
                MaxConnectorExecutionsPerDay = int.MaxValue,
                MaxPdfRendersPerDay = int.MaxValue
            };
            return quota;
        }
    }
}
