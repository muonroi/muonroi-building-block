namespace Muonroi.RuleEngine.Proliferation.Persistence.Entities;

/// <summary>
/// Persistence model for a generated proliferation scenario.
/// </summary>
public sealed class NeuronScenarioEntity
{
    /// <summary>Gets or sets the scenario identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the seed rule code.</summary>
    public string SeedRuleCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the scenario name.</summary>
    public string ScenarioName { get; set; } = string.Empty;

    /// <summary>Gets or sets the scenario type.</summary>
    public ScenarioType Type { get; set; }

    /// <summary>Gets or sets the scenario scope.</summary>
    public ProliferationScope Scope { get; set; }

    /// <summary>Gets or sets the parent scenario identifier.</summary>
    public string? ParentScenarioId { get; set; }

    /// <summary>Gets or sets the generation depth.</summary>
    public int GenerationDepth { get; set; }

    /// <summary>Gets or sets the proliferation reason.</summary>
    public string ProliferationReason { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized input facts.</summary>
    public string InputFactsJson { get; set; } = "{}";

    /// <summary>Gets or sets the expected behavior.</summary>
    public string? ExpectedBehavior { get; set; }

    /// <summary>Gets or sets the generated rule-flow graph.</summary>
    public string? GeneratedRuleFlowGraph { get; set; }

    /// <summary>Gets or sets the scenario status.</summary>
    public ScenarioStatus Status { get; set; }

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public string? TenantId { get; set; }
}
