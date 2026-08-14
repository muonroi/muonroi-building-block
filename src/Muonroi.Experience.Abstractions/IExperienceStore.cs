namespace Muonroi.Experience.Abstractions;

/// <summary>
/// Persists, retrieves, and manages NeuronExperience entries across the 4-tier storage hierarchy.
/// Implementations may use Qdrant (semantic search), file system (zero-dep fallback), or in-memory.
/// </summary>
public interface IExperienceStore
{
    /// <summary>
    /// Stores a new experience entry in the tier specified by <see cref="NeuronExperience.Tier"/>.
    /// Returns false if the entry was rejected (budget exceeded or duplicate detected).
    /// </summary>
    Task<bool> StoreAsync(NeuronExperience experience, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the top-K most semantically relevant entries for the given query string.
    /// Relevance scoring strategy is implementation-defined (cosine similarity, keyword, etc.).
    /// </summary>
    Task<IEnumerable<ExperienceSearchResult>> FindRelevantAsync(
        string query,
        int topK = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Promotes an entry one tier upward (e.g., Tier 2 SelfQA → Tier 1 Behavioral).
    /// Returns the updated entry after promotion.
    /// </summary>
    Task<NeuronExperience> PromoteAsync(NeuronExperience experience, CancellationToken ct = default);

    /// <summary>
    /// Demotes an entry one tier downward and resets its HitCount to zero.
    /// Returns the updated entry after demotion.
    /// </summary>
    Task<NeuronExperience> DemoteAsync(NeuronExperience experience, CancellationToken ct = default);

    /// <summary>
    /// Clusters semantically similar Tier 2 entries by concept and abstracts them into a single Tier 0 principle.
    /// Returns the newly created Tier 0 principle entry.
    /// </summary>
    Task<NeuronExperience> ClusterAndAbstractAsync(
        IEnumerable<NeuronExperience> tier2Entries,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all entries stored in the specified tier.
    /// Used by the evolution orchestrator for promotion and archival sweeps.
    /// </summary>
    Task<IEnumerable<NeuronExperience>> FindAllInTierAsync(ExperienceTier tier, CancellationToken ct = default);

    /// <summary>
    /// Deletes the entry with the given id from whichever tier it occupies.
    /// No-op if not found.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
