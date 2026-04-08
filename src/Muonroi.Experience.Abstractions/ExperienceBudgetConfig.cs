namespace Muonroi.Experience.Abstractions;

/// <summary>Token budget and dedup configuration for the experience store.</summary>
public sealed record ExperienceBudgetConfig
{
    /// <summary>Max tokens for Tier 0 Principle entries. Default: 400.</summary>
    public int PrincipleBudget { get; init; } = 400;
    /// <summary>Max tokens for Tier 1 Behavioral entries. Default: 600.</summary>
    public int BehavioralBudget { get; init; } = 600;
    /// <summary>Max tokens for Tier 2 Self-QA entries. Default: 500.</summary>
    public int SelfQABudget { get; init; } = 500;
    /// <summary>Cosine similarity threshold above which a new entry is considered a duplicate. Default: 0.85.</summary>
    public float DedupThreshold { get; init; } = 0.85f;
    /// <summary>Initial confidence range minimum for newly extracted entries. Default: 0.4.</summary>
    public float InitialConfidenceMin { get; init; } = 0.4f;
    /// <summary>Initial confidence range maximum for newly extracted entries. Default: 0.6.</summary>
    public float InitialConfidenceMax { get; init; } = 0.6f;
    /// <summary>Number of confirmed hits before a Tier 2 entry is promoted to Tier 1. Default: 3.</summary>
    public int PromotionHitThreshold { get; init; } = 3;
    /// <summary>Days after which a Tier 2 entry with zero hits is archived. Default: 90.</summary>
    public int ArchivalDaysThreshold { get; init; } = 90;
}
