using System.Text.Json;

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
    public IReadOnlyDictionary<string, int> BySeedRule { get; init; } = new Dictionary<string, int>();
}

public sealed record ProliferationContext
{
    public ProliferationScope Scope { get; init; } = ProliferationScope.Rule;
    public int CurrentDepth { get; init; }
    public int RemainingBudget { get; init; }
    public IReadOnlyList<string>? FocusAreas { get; init; }
    public string? TenantId { get; init; }
}
