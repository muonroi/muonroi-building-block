namespace Muonroi.Tenancy.Abstractions.Models;

public sealed class TenantQuota
{
    public string TenantId { get; set; } = string.Empty;
    public int MaxRulesPerTenant { get; set; } = 100;
    public int MaxRuleExecutionsPerDay { get; set; } = 10_000;
    public int MaxConcurrentExecutions { get; set; } = 10;
    public int MaxDecisionTables { get; set; } = 50;
    public int MaxJsonWorkflows { get; set; } = 100;
    public int MaxStorageMB { get; set; } = 100;
    public int MaxApiRequestsPerMinute { get; set; } = 100;
    public int MaxRuleEvaluationsPerSecond { get; set; } = 50;
    public int MaxWorkflowExecutionsPerHour { get; set; } = 500;
    public int MaxRuleComplexity { get; set; } = 10;
    public int MaxWorkflowSizeKB { get; set; } = 500;
    public int MaxExecutionTimeMs { get; set; } = 5000;
    public int MaxMessagesPerDay { get; set; } = 10_000;
    public int MaxMessagesPerMinute { get; set; } = 500;
    public TenantTier Tier { get; set; } = TenantTier.Free;
}

public enum TenantTier
{
    Free,
    Starter,
    Professional,
    Enterprise
}

public static class TenantQuotaPresets
{
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
                MaxMessagesPerMinute = 50
            };
            return free;
        }
    }

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
                MaxMessagesPerMinute = 200
            };
            return quota;
        }
    }

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
                MaxMessagesPerMinute = 1000
            };
            return quota;
        }
    }

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
                MaxMessagesPerMinute = int.MaxValue
            };
            return quota;
        }
    }
}
