using System.Text.Json;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Models;

/// <summary>Scope of a proliferation run.</summary>
public enum ProliferationScope
{
    /// <summary>Single-rule scope.</summary>
    Rule = 0,
    /// <summary>Workflow scope.</summary>
    Workflow = 1,
    /// <summary>Cross-rule scope.</summary>
    CrossRule = 2
}

/// <summary>Scenario classification.</summary>
public enum ScenarioType
{
    /// <summary>Business-facing scenario.</summary>
    Business = 0,
    /// <summary>Technical scenario.</summary>
    Technical = 1
}

/// <summary>Lifecycle state of a scenario.</summary>
public enum ScenarioStatus
{
    /// <summary>Scenario has not started yet.</summary>
    Pending = 0,
    /// <summary>Scenario is currently running.</summary>
    Running = 1,
    /// <summary>Scenario passed validation or execution.</summary>
    Passed = 2,
    /// <summary>Scenario failed validation or execution.</summary>
    Failed = 3,
    /// <summary>Scenario ended with an error.</summary>
    Error = 4,
    /// <summary>Scenario was skipped.</summary>
    Skipped = 5
}

/// <summary>Scenario proposed by the proliferation engine.</summary>
public sealed record NeuronScenario
{
    /// <summary>Scenario identifier.</summary>
    public required string Id { get; init; }
    /// <summary>Seed rule code that produced the scenario.</summary>
    public required string SeedRuleCode { get; init; }
    /// <summary>Human-readable scenario name.</summary>
    public required string ScenarioName { get; init; }
    /// <summary>Scenario type.</summary>
    public ScenarioType Type { get; init; }
    /// <summary>Proliferation scope.</summary>
    public ProliferationScope Scope { get; init; }
    /// <summary>Parent scenario identifier, if any.</summary>
    public string? ParentScenarioId { get; init; }
    /// <summary>Generation depth.</summary>
    public int GenerationDepth { get; init; }
    /// <summary>Reason the scenario was generated.</summary>
    public required string ProliferationReason { get; init; }
    /// <summary>Input facts supplied to the scenario.</summary>
    public JsonElement InputFacts { get; init; }
    /// <summary>Expected behavior description, if any.</summary>
    public string? ExpectedBehavior { get; init; }
    /// <summary>Generated rule flow graph, if any.</summary>
    public string? GeneratedRuleFlowGraph { get; init; }
    /// <summary>Current scenario status.</summary>
    public ScenarioStatus Status { get; init; }
    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>Tenant identifier, if any.</summary>
    public string? TenantId { get; init; }
}

/// <summary>Execution result for a scenario.</summary>
public sealed record ScenarioResult
{
    /// <summary>Scenario identifier.</summary>
    public required string ScenarioId { get; init; }
    /// <summary>Whether execution succeeded.</summary>
    public bool IsSuccess { get; init; }
    /// <summary>Whether the execution matched the expectation.</summary>
    public bool MatchesExpectation { get; init; }
    /// <summary>Observed behavior string, if any.</summary>
    public string? ActualBehavior { get; init; }
    /// <summary>Output facts produced by the scenario.</summary>
    public JsonElement? OutputFacts { get; init; }
    /// <summary>Error messages emitted by the scenario.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
    /// <summary>Total execution duration.</summary>
    public TimeSpan Duration { get; init; }
    /// <summary>Execution timestamp.</summary>
    public DateTimeOffset ExecutedAt { get; init; }
}

/// <summary>Plan for a proliferation run.</summary>
public sealed record ProliferationPlan
{
    /// <summary>Seed rule code.</summary>
    public required string SeedRuleCode { get; init; }
    /// <summary>Proliferation scope.</summary>
    public ProliferationScope Scope { get; init; }
    /// <summary>Generated scenarios.</summary>
    public IReadOnlyList<NeuronScenario> Scenarios { get; init; } = [];
    /// <summary>AI model used to generate the plan.</summary>
    public required string AiModelUsed { get; init; }
    /// <summary>Generation duration.</summary>
    public TimeSpan GenerationDuration { get; init; }
}

/// <summary>Lineage information for a generated scenario.</summary>
public sealed record RuleLineage
{
    /// <summary>Scenario identifier.</summary>
    public required string ScenarioId { get; init; }
    /// <summary>Seed rule code.</summary>
    public required string SeedRuleCode { get; init; }
    /// <summary>Parent scenario identifier, if any.</summary>
    public string? ParentScenarioId { get; init; }
    /// <summary>Lineage depth.</summary>
    public int Depth { get; init; }
    /// <summary>Reason for the scenario.</summary>
    public required string Reason { get; init; }
    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Aggregate proliferation statistics.</summary>
public sealed record ProliferationStats
{
    /// <summary>Total scenarios.</summary>
    public int TotalScenarios { get; init; }
    /// <summary>Number of passing scenarios.</summary>
    public int Passed { get; init; }
    /// <summary>Number of failing scenarios.</summary>
    public int Failed { get; init; }
    /// <summary>Number of pending scenarios.</summary>
    public int Pending { get; init; }
    /// <summary>Maximum depth reached.</summary>
    public int MaxDepthReached { get; init; }
    /// <summary>Scenarios generated from feedback.</summary>
    public int FeedbackGenerated { get; init; }
    /// <summary>Deduplicated scenario count.</summary>
    public int Deduplicated { get; init; }
    /// <summary>Scenario counts by seed rule.</summary>
    public IReadOnlyDictionary<string, int> BySeedRule { get; init; } = new Dictionary<string, int>();
    /// <summary>Scenario counts by tenant.</summary>
    public IReadOnlyDictionary<string, TenantSimulationStats> ByTenant { get; init; } = new Dictionary<string, TenantSimulationStats>();
}

/// <summary>Execution context for proliferation operations.</summary>
public sealed record ProliferationContext
{
    /// <summary>Proliferation scope.</summary>
    public ProliferationScope Scope { get; init; } = ProliferationScope.Rule;
    /// <summary>Current recursion depth.</summary>
    public int CurrentDepth { get; init; }
    /// <summary>Remaining scenario budget.</summary>
    public int RemainingBudget { get; init; }
    /// <summary>Optional focus areas for generation.</summary>
    public IReadOnlyList<string>? FocusAreas { get; init; }
    /// <summary>Tenant identifier, if any.</summary>
    public string? TenantId { get; init; }
    /// <summary>Detected ruleset kind.</summary>
    public RuleSetKind RuleSetKind { get; init; } = RuleSetKind.Unknown;
    /// <summary>Flow graph path analysis for FlowGraph rulesets.</summary>
    public FlowGraphAnalysis? FlowAnalysis { get; init; }
    /// <summary>Cross-rule interaction analysis for FlowGraph rulesets.</summary>
    public CrossRuleAnalysis? CrossRuleAnalysis { get; init; }
}

/// <summary>Actionable recommendation for a workflow's coverage and quality gaps.</summary>
public sealed record ProliferationSuggestion
{
    /// <summary>Workflow name.</summary>
    public required string WorkflowName { get; init; }
    /// <summary>Field coverage percentage.</summary>
    public double FieldCoveragePercent { get; init; }
    /// <summary>Flow node coverage percentage, if applicable.</summary>
    public double? FlowNodeCoveragePercent { get; init; }
    /// <summary>Flow edge coverage percentage, if applicable.</summary>
    public double? FlowEdgeCoveragePercent { get; init; }
    /// <summary>Fields that remain uncovered.</summary>
    public IReadOnlyList<string> UncoveredFields { get; init; } = [];
    /// <summary>Nodes that remain uncovered.</summary>
    public IReadOnlyList<string> UncoveredNodes { get; init; } = [];
    /// <summary>Suggested areas to focus on.</summary>
    public IReadOnlyList<string> SuggestedFocusAreas { get; init; } = [];
    /// <summary>Total scenarios in the suggestion context.</summary>
    public int TotalScenarios { get; init; }
    /// <summary>Number of passing scenarios.</summary>
    public int PassedScenarios { get; init; }
    /// <summary>Number of failing scenarios.</summary>
    public int FailedScenarios { get; init; }
    /// <summary>Final recommendation text.</summary>
    public required string Recommendation { get; init; }
}

/// <summary>Per-tenant simulation statistics for multi-tenant traffic simulation.</summary>
public sealed record TenantSimulationStats
{
    /// <summary>Tenant identifier.</summary>
    public required string TenantId { get; init; }
    /// <summary>Total scenarios.</summary>
    public int TotalScenarios { get; init; }
    /// <summary>Passing scenarios.</summary>
    public int Passed { get; init; }
    /// <summary>Failing scenarios.</summary>
    public int Failed { get; init; }
    /// <summary>Pass rate percentage.</summary>
    public double PassRate => TotalScenarios > 0 ? Math.Round((double)Passed / TotalScenarios * 100, 1) : 0;
}

/// <summary>
/// Result of a CI/CD synchronous proliferation run for pipeline gate integration.
/// Returns pass/fail based on coverage threshold and failure tolerance.
/// </summary>
public sealed record CiRunResult
{
    /// <summary>True if coverage is at least minCoverage and failedCount is at most maxFailures.</summary>
    public bool Passed { get; init; }
    /// <summary>Field coverage percentage (0-100).</summary>
    public double FieldCoveragePercent { get; init; }
    /// <summary>Flow node coverage percentage. Null for non-flow rulesets.</summary>
    public double? FlowNodeCoveragePercent { get; init; }
    /// <summary>Total number of scenarios evaluated.</summary>
    public int TotalScenarios { get; init; }
    /// <summary>Number of scenarios that passed.</summary>
    public int PassedScenarios { get; init; }
    /// <summary>Number of scenarios that failed.</summary>
    public int FailedScenarios { get; init; }
    /// <summary>Total wall-clock duration of the CI run.</summary>
    public TimeSpan Duration { get; init; }
    /// <summary>Failure messages from failed scenarios. Empty when all pass.</summary>
    public IReadOnlyList<string> FailureMessages { get; init; } = [];
    /// <summary>Optional URL to the full test report in the dashboard.</summary>
    public string? ReportUrl { get; init; }
}
