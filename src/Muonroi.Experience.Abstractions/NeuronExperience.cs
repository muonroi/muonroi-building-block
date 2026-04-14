namespace Muonroi.Experience.Abstractions;

/// <summary>
/// An immutable experience entry extracted from an agent session trajectory.
/// Represents a single unit of learning: what triggered a mistake, what was done, and why it works.
/// </summary>
public sealed record NeuronExperience
{
    /// <summary>Unique identifier for this experience entry.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// Short phrase describing the situation that triggered this experience
    /// (e.g., "editing file without reading first", "blind-fixing without diagnosis").
    /// </summary>
    public required string Trigger { get; init; }

    /// <summary>The mistake or question this experience addresses.</summary>
    public required string Question { get; init; }

    /// <summary>Step-by-step reasoning chain explaining why the solution works.</summary>
    public required string[] Reasoning { get; init; }

    /// <summary>The correct action or pattern to apply in future.</summary>
    public required string Solution { get; init; }

    /// <summary>
    /// Optional generalized principle abstracted from this entry
    /// (populated when entry is promoted to Tier 0).
    /// </summary>
    public string? Principle { get; init; }

    /// <summary>
    /// Confidence score in the 0.0–1.0 range.
    /// Newly extracted entries start in the 0.4–0.6 range per ExperienceBudgetConfig.
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>Number of times this entry has been confirmed as relevant by the interceptor.</summary>
    public int HitCount { get; init; }

    /// <summary>Storage tier this entry belongs to.</summary>
    public ExperienceTier Tier { get; init; }

    /// <summary>Session or source identifier that produced this entry (for attribution).</summary>
    public required string CreatedFrom { get; init; }

    /// <summary>UTC timestamp when this entry was extracted.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
