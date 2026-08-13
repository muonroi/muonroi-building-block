namespace Muonroi.RuleEngine.Proliferation.Persistence.Entities;

/// <summary>
/// Persistence model for a scenario lineage entry.
/// </summary>
public sealed class RuleLineageEntity
{
    /// <summary>Gets or sets the scenario identifier.</summary>
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>Gets or sets the seed rule code.</summary>
    public string SeedRuleCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent scenario identifier.</summary>
    public string? ParentScenarioId { get; set; }

    /// <summary>Gets or sets the lineage depth.</summary>
    public int Depth { get; set; }

    /// <summary>Gets or sets the lineage reason.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
