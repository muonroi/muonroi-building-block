using System.Text.Json;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Models;

public enum ProliferationScope { Rule = 0, Workflow = 1, CrossRule = 2 }

public enum ScenarioType { Business = 0, Technical = 1 }

public enum ScenarioStatus { Pending = 0, Running = 1, Passed = 2, Failed = 3, Error = 4, Skipped = 5 }

public sealed record NeuronScenario
{
    public required string Id { get; init; }
    public required string SeedRuleCode { get; init; }
    public required string ScenarioName { get; init; }
    public ScenarioType Type { get; init; }
    public ProliferationScope Scope { get; init; }
    public string? ParentScenarioId { get; init; }
    public int GenerationDepth { get; init; }
    public required string ProliferationReason { get; init; }
    public JsonElement InputFacts { get; init; }
    public string? ExpectedBehavior { get; init; }
    public string? GeneratedRuleFlowGraph { get; init; }
    public ScenarioStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? TenantId { get; init; }
}

public sealed record ScenarioResult
{
    public required string ScenarioId { get; init; }
    public bool IsSuccess { get; init; }
    public bool MatchesExpectation { get; init; }
    public string? ActualBehavior { get; init; }
    public JsonElement? OutputFacts { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public TimeSpan Duration { get; init; }
    public DateTimeOffset ExecutedAt { get; init; }
}

public sealed record ProliferationPlan
{
    public required string SeedRuleCode { get; init; }
    public ProliferationScope Scope { get; init; }
    public IReadOnlyList<NeuronScenario> Scenarios { get; init; } = [];
    public required string AiModelUsed { get; init; }
    public TimeSpan GenerationDuration { get; init; }
}

public sealed record RuleLineage
{
    public required string ScenarioId { get; init; }
    public required string SeedRuleCode { get; init; }
    public string? ParentScenarioId { get; init; }
    public int Depth { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record ProliferationStats
{
    public int TotalScenarios { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Pending { get; init; }
    public int MaxDepthReached { get; init; }
    public int FeedbackGenerated { get; init; }
    public int Deduplicated { get; init; }
    public IReadOnlyDictionary<string, int> BySeedRule { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, TenantSimulationStats> ByTenant { get; init; } = new Dictionary<string, TenantSimulationStats>();
}

public sealed record ProliferationContext
{
    public ProliferationScope Scope { get; init; } = ProliferationScope.Rule;
    public int CurrentDepth { get; init; }
    public int RemainingBudget { get; init; }
    public IReadOnlyList<string>? FocusAreas { get; init; }
    public string? TenantId { get; init; }
    public RuleSetKind RuleSetKind { get; init; } = RuleSetKind.Unknown;

    /// <summary>Flow graph path analysis — populated for FlowGraph rulesets (Task 3).</summary>
    public FlowGraphAnalysis? FlowAnalysis { get; init; }

    /// <summary>Cross-rule interaction analysis — populated for FlowGraph rulesets (Task 4).</summary>
    public CrossRuleAnalysis? CrossRuleAnalysis { get; init; }
}

/// <summary>
/// Actionable recommendation for a workflow's coverage and quality gaps.
/// </summary>
public sealed record ProliferationSuggestion
{
    public required string WorkflowName { get; init; }
    public double FieldCoveragePercent { get; init; }
    public double? FlowNodeCoveragePercent { get; init; }
    public double? FlowEdgeCoveragePercent { get; init; }
    public IReadOnlyList<string> UncoveredFields { get; init; } = [];
    public IReadOnlyList<string> UncoveredNodes { get; init; } = [];
    public IReadOnlyList<string> SuggestedFocusAreas { get; init; } = [];
    public int TotalScenarios { get; init; }
    public int PassedScenarios { get; init; }
    public int FailedScenarios { get; init; }
    public required string Recommendation { get; init; }
}

/// <summary>
/// Per-tenant simulation statistics for multi-tenant traffic simulation.
/// </summary>
public sealed record TenantSimulationStats
{
    public required string TenantId { get; init; }
    public int TotalScenarios { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public double PassRate => TotalScenarios > 0 ? Math.Round((double)Passed / TotalScenarios * 100, 1) : 0;
}

/// <summary>
/// Result of a CI/CD synchronous proliferation run for pipeline gate integration.
/// Returns pass/fail based on coverage threshold and failure tolerance.
/// </summary>
public sealed record CiRunResult
{
    /// <summary>True if coverage >= minCoverage AND failedCount <= maxFailures.</summary>
    public bool Passed { get; init; }

    /// <summary>Field coverage percentage (0–100).</summary>
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
