namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Embeds text into a vector for semantic similarity comparison.
/// Used by VectorSemanticDeduplicator for fuzzy scenario dedup.
/// </summary>
public interface IVectorEmbedder
{
    /// <summary>Embeds the supplied text into a numeric vector.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
