namespace Muonroi.Experience.Runtime;

/// <summary>
/// Coordinates tier routing for the Experience Engine.
/// Receives a <see cref="NeuronExperience"/> and routes it to the correct tier
/// in the registered <see cref="IExperienceStore"/> based on <see cref="NeuronExperience.Tier"/>.
/// </summary>
/// <remarks>
/// The orchestrator is a thin coordinator — the actual tier routing logic lives inside
/// the concrete store implementations (FileExperienceStore / QdrantExperienceStore).
/// The orchestrator provides a named entry point for consumers and adds observability via IMLog.
/// </remarks>
/// <remarks>
/// Initialises a new <see cref="ExperienceStoreOrchestrator"/> with the registered store.
/// </remarks>
public sealed class ExperienceStoreOrchestrator(IExperienceStore store, IMLog<ExperienceStoreOrchestrator>? log = null)
{
    private readonly IExperienceStore _store = MGuard.NotNull(store);
    private readonly IMLog<ExperienceStoreOrchestrator>? _log = log;

    /// <summary>
    /// Routes a <see cref="NeuronExperience"/> to the correct tier in the registered store.
    /// The tier is determined by <see cref="NeuronExperience.Tier"/>.
    /// </summary>
    /// <returns><c>true</c> if the entry was stored; <c>false</c> if the tier budget was exceeded.</returns>
    public async Task<bool> RouteAndStoreAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        _log?.LogInformation("Routing experience {Id} to tier {Tier}", experience.Id, experience.Tier);
        return await _store.StoreAsync(experience, ct);
    }

    /// <summary>
    /// Delegates to the underlying store's <see cref="IExperienceStore.FindRelevantAsync"/>.
    /// </summary>
    public Task<IEnumerable<ExperienceSearchResult>> FindRelevantAsync(string query, int topK = 5, CancellationToken ct = default)
        => _store.FindRelevantAsync(query, topK, ct);

    /// <summary>
    /// Promotes an experience to a higher tier (lower tier number).
    /// </summary>
    public Task<NeuronExperience> PromoteAsync(NeuronExperience experience, CancellationToken ct = default)
        => _store.PromoteAsync(experience, ct);

    /// <summary>
    /// Demotes an experience to a lower tier (higher tier number).
    /// </summary>
    public Task<NeuronExperience> DemoteAsync(NeuronExperience experience, CancellationToken ct = default)
        => _store.DemoteAsync(experience, ct);

    /// <summary>
    /// Clusters the supplied Tier 2 entries and abstracts them into a single Tier 0 principle.
    /// Delegates to <see cref="IExperienceStore.ClusterAndAbstractAsync"/>.
    /// Requires an <see cref="IExperienceBrain"/> to be registered via AddExperienceBrain().
    /// </summary>
    public Task<NeuronExperience> ClusterAndAbstractAsync(IEnumerable<NeuronExperience> tier2Entries, CancellationToken ct = default)
        => _store.ClusterAndAbstractAsync(tier2Entries, ct);

    /// <summary>
    /// Returns all entries stored in the specified tier.
    /// Used by the evolution orchestrator for promotion and archival sweeps.
    /// </summary>
    public Task<IEnumerable<NeuronExperience>> FindAllInTierAsync(ExperienceTier tier, CancellationToken ct = default)
        => _store.FindAllInTierAsync(tier, ct);

    /// <summary>
    /// Deletes the entry with the given id from whichever tier it occupies.
    /// No-op if not found.
    /// </summary>
    public Task DeleteAsync(string id, CancellationToken ct = default)
        => _store.DeleteAsync(id, ct);
}
