namespace Muonroi.Experience.Runtime.Evolution;

/// <summary>
/// Snapshot of a single evolution run: token counts, compression ratio, and event counters.
/// CompressionRatio &lt; 1.0 means memory SHRANK — the primary observable invariant.
/// </summary>
public sealed record EvolutionReport
{
    /// <summary>UTC timestamp when the evolution run started.</summary>
    public DateTimeOffset RunAt { get; init; }

    /// <summary>Total estimated tokens across all Tier 2 entries before this run.</summary>
    public int TokensBefore { get; set; }

    /// <summary>Total estimated tokens across all Tier 2 entries after this run.</summary>
    public int TokensAfter { get; set; }

    /// <summary>
    /// Ratio of tokens after to tokens before. Values below 1.0 indicate compression.
    /// A ratio of 1.0 means no change; values above 1.0 should not occur in normal operation.
    /// </summary>
    public float CompressionRatio { get; set; }

    /// <summary>Number of Tier 2 entries promoted to Tier 1 during this run.</summary>
    public int PromotedCount { get; set; }

    /// <summary>Number of Tier 2 entries moved to the archive during this run.</summary>
    public int ArchivedCount { get; set; }

    /// <summary>Number of clusters formed from Tier 2 entries during this run.</summary>
    public int ClustersFormed { get; set; }

    /// <summary>Number of Tier 0 principles successfully stored during this run.</summary>
    public int PrinciplesCreated { get; set; }

    /// <inheritdoc/>
    public override string ToString() =>
        $"Evolution Run: {RunAt:yyyy-MM-dd}\n" +
        $"├── Promoted: {PromotedCount}\n" +
        $"├── Clustered: {ClustersFormed} clusters → {PrinciplesCreated} principles\n" +
        $"├── Archived: {ArchivedCount}\n" +
        $"├── Tokens before: {TokensBefore}\n" +
        $"├── Tokens after: {TokensAfter}\n" +
        $"└── Compression: {CompressionRatio:F2}x";
}
