namespace Muonroi.Quota.Abstractions;

/// <summary>
/// Defines the types of quotas that can be tracked for a tenant.
/// </summary>
public enum QuotaType
{
    /// <summary>Maximum number of rule executions per day.</summary>
    RuleExecutionsPerDay,
    /// <summary>Maximum number of concurrent executions allowed.</summary>
    ConcurrentExecutions,
    /// <summary>Maximum number of API requests per minute.</summary>
    ApiRequestsPerMinute,
    /// <summary>Maximum number of rule evaluations per second.</summary>
    RuleEvaluationsPerSecond,
    /// <summary>Maximum number of workflow executions per hour.</summary>
    WorkflowExecutionsPerHour,
    /// <summary>Total storage usage allowed in Megabytes.</summary>
    StorageUsageMB,
    /// <summary>Total number of rules allowed.</summary>
    TotalRules,
    /// <summary>Total number of decision tables allowed.</summary>
    TotalDecisionTables,
    /// <summary>Total number of workflows allowed.</summary>
    TotalWorkflows,
    /// <summary>Maximum number of messages processed per day.</summary>
    MessagesPerDay,
    /// <summary>Maximum number of messages processed per minute.</summary>
    MessagesPerMinute,
    /// <summary>Total number of connectors allowed.</summary>
    TotalConnectors,
    /// <summary>Maximum number of connector executions per day.</summary>
    ConnectorExecutionsPerDay
}
