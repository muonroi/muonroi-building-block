namespace Muonroi.RuleEngine.Abstractions;

public enum QuotaType
{
    RuleExecutionsPerDay,
    ConcurrentExecutions,
    ApiRequestsPerMinute,
    RuleEvaluationsPerSecond,
    WorkflowExecutionsPerHour,
    StorageUsageMB,
    TotalRules,
    TotalDecisionTables,
    TotalWorkflows
}
