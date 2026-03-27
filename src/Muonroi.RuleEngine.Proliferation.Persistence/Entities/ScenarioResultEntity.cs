namespace Muonroi.RuleEngine.Proliferation.Persistence.Entities;

/// <summary>
/// Persistence model for a scenario execution result.
/// </summary>
public sealed class ScenarioResultEntity
{
    /// <summary>Gets or sets the scenario identifier.</summary>
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>Gets or sets whether execution succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Gets or sets whether the result matched expectations.</summary>
    public bool MatchesExpectation { get; set; }

    /// <summary>Gets or sets the actual behavior text.</summary>
    public string? ActualBehavior { get; set; }

    /// <summary>Gets or sets the serialized output facts.</summary>
    public string? OutputFactsJson { get; set; }

    /// <summary>Gets or sets the serialized error list.</summary>
    public string ErrorsJson { get; set; } = "[]";

    /// <summary>Gets or sets the execution duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Gets or sets the execution timestamp.</summary>
    public DateTimeOffset ExecutedAt { get; set; }
}
