using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime.Internal;
using Muonroi.Logging.Abstractions;
using Qdrant.Client.Grpc;

namespace Muonroi.Experience.Runtime.Qdrant;

/// <summary>
/// <see cref="IExperienceStore"/> implementation backed by a Qdrant vector database.
/// Uses one collection per <see cref="ExperienceTier"/> (mapped via <see cref="TierCollectionNames"/>).
/// All Qdrant I/O goes through <see cref="IQdrantClientWrapper"/> for testability.
/// </summary>
public sealed class QdrantExperienceStore : IExperienceStore
{
    private readonly IQdrantClientWrapper _client;
    private readonly ExperienceStoreOptions _options;
    private readonly ExperienceBudgetConfig _budget;
    private readonly IExperienceBrain? _brain;
    private readonly IMLog<QdrantExperienceStore>? _log;

    /// <summary>Tracks which collection names have already been ensured to avoid redundant CreateCollectionAsync calls.</summary>
    private readonly HashSet<string> _ensuredCollections = new(StringComparer.Ordinal);

    private static readonly ExperienceTier[] SearchableTiers =
    [
        ExperienceTier.Principle,
        ExperienceTier.Behavioral,
        ExperienceTier.SelfQA
    ];

    /// <summary>
    /// Initialises a new <see cref="QdrantExperienceStore"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <see cref="ExperienceStoreOptions.VectorSize"/> is not greater than zero.</exception>
    public QdrantExperienceStore(
        IQdrantClientWrapper client,
        IOptions<ExperienceStoreOptions> options,
        IMLog<QdrantExperienceStore>? log = null,
        IExperienceBrain? brain = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _budget = _options.Budget;
        _brain = brain;
        _log = log;

        if (_options.VectorSize <= 0)
        {
            throw new ArgumentException("VectorSize must be > 0", nameof(options));
        }
    }

    // -------------------------------------------------------------------------
    // IExperienceStore
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<bool> StoreAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        string collection = TierCollectionNames.For(experience.Tier);
        await EnsureCollectionExistsAsync(collection, ct);

        if (experience.Tier != ExperienceTier.RawTrajectory)
        {
            IEnumerable<NeuronExperience> existing = await FindAllInTierAsync(experience.Tier, ct);
            int budget = TokenBudgetEnforcer.GetBudgetForTier(experience.Tier, _budget);
            if (TokenBudgetEnforcer.WouldExceedBudget(existing, experience, budget))
            {
                _log?.LogWarning(
                    "Budget exceeded for tier {Tier}, rejecting entry {Id}",
                    experience.Tier, experience.Id);
                return false;
            }
        }

        var point = new PointStruct
        {
            Id = new PointId { Uuid = experience.Id },
            /// <remarks>Zero-vector placeholder — real embedding vectors are injected by Phase 96 (Experience Extraction) via IExperienceBrain.</remarks>
            Vectors = new float[_options.VectorSize]
        };
        point.Payload["json"] = JsonSerializer.Serialize(experience);

        await _client.UpsertAsync(collection, new[] { point }, ct);
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="query"/> must be a JSON-encoded float array matching <see cref="ExperienceStoreOptions.VectorSize"/>.
    /// Plain text queries return an empty result set because the Qdrant store has no keyword index.
    /// </remarks>
    public async Task<IEnumerable<ExperienceSearchResult>> FindRelevantAsync(
        string query,
        int topK = 5,
        CancellationToken ct = default)
    {
        float[]? queryVector;
        try
        {
            queryVector = JsonSerializer.Deserialize<float[]>(query);
        }
        catch
        {
            return Enumerable.Empty<ExperienceSearchResult>();
        }

        if (queryVector is null || queryVector.Length == 0)
        {
            return Enumerable.Empty<ExperienceSearchResult>();
        }

        var allHits = new List<(ScoredPoint Hit, NeuronExperience Experience)>();

        foreach (ExperienceTier tier in SearchableTiers)
        {
            string collection = TierCollectionNames.For(tier);
            await EnsureCollectionExistsAsync(collection, ct);

            IReadOnlyList<ScoredPoint> hits = await _client.SearchAsync(collection, queryVector, (ulong)topK, ct);
            foreach (ScoredPoint hit in hits)
            {
                if (!hit.Payload.TryGetValue("json", out Value? jsonValue))
                {
                    continue;
                }

                NeuronExperience? entry = TryDeserializeExperience(jsonValue.StringValue);
                if (entry is not null)
                {
                    allHits.Add((hit, entry));
                }
            }
        }

        return allHits
            .OrderByDescending(x => x.Hit.Score)
            .Take(topK)
            .Select(x => new ExperienceSearchResult(x.Experience, x.Hit.Score));
    }

    /// <inheritdoc />
    public async Task<NeuronExperience> PromoteAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        if (experience.Tier == ExperienceTier.Principle)
        {
            throw new InvalidOperationException("Cannot promote a Tier 0 (Principle) entry — it is already the highest tier.");
        }

        var targetTier = (ExperienceTier)((int)experience.Tier - 1);
        NeuronExperience promoted = experience with { Tier = targetTier };

        // Delete from source collection.
        await _client.DeleteAsync(TierCollectionNames.For(experience.Tier), Guid.Parse(experience.Id), ct);

        // Upsert into target collection.
        string targetCollection = TierCollectionNames.For(targetTier);
        await EnsureCollectionExistsAsync(targetCollection, ct);
        var point = new PointStruct
        {
            Id = new PointId { Uuid = promoted.Id },
            Vectors = new float[_options.VectorSize]
        };
        point.Payload["json"] = JsonSerializer.Serialize(promoted);
        await _client.UpsertAsync(targetCollection, new[] { point }, ct);

        return promoted;
    }

    /// <inheritdoc />
    public async Task<NeuronExperience> DemoteAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        if (experience.Tier == ExperienceTier.RawTrajectory)
        {
            throw new InvalidOperationException("Cannot demote a Tier 3 (RawTrajectory) entry — it is already the lowest tier.");
        }

        var targetTier = (ExperienceTier)((int)experience.Tier + 1);
        NeuronExperience demoted = experience with { Tier = targetTier, HitCount = 0 };

        await _client.DeleteAsync(TierCollectionNames.For(experience.Tier), Guid.Parse(experience.Id), ct);

        string targetCollection = TierCollectionNames.For(targetTier);
        await EnsureCollectionExistsAsync(targetCollection, ct);
        var point = new PointStruct
        {
            Id = new PointId { Uuid = demoted.Id },
            Vectors = new float[_options.VectorSize]
        };
        point.Payload["json"] = JsonSerializer.Serialize(demoted);
        await _client.UpsertAsync(targetCollection, new[] { point }, ct);

        return demoted;
    }

    /// <inheritdoc />
    /// <remarks>ClusterAndAbstractAsync requires an IExperienceBrain — register via AddExperienceBrain() before use.</remarks>
    public Task<NeuronExperience> ClusterAndAbstractAsync(IEnumerable<NeuronExperience> tier2Entries, CancellationToken ct = default)
    {
        if (_brain is null)
        {
            throw new NotSupportedException("IExperienceBrain not registered — call AddExperienceBrain() before using ClusterAndAbstractAsync");
        }

        // ClusterAndAbstractAsync full implementation is part of Phase 98 Plan 02 (Evolution Orchestrator).
        throw new NotSupportedException("ClusterAndAbstractAsync full implementation is provided by ExperienceEvolutionOrchestrator (Phase 98 Plan 02)");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NeuronExperience>> FindAllInTierAsync(ExperienceTier tier, CancellationToken ct = default)
    {
        string collection = TierCollectionNames.For(tier);
        await EnsureCollectionExistsAsync(collection, ct);

        IReadOnlyList<RetrievedPoint> points = await _client.ScrollAsync(collection, limit: 1000, ct);

        var results = new List<NeuronExperience>();
        foreach (RetrievedPoint point in points)
        {
            if (!point.Payload.TryGetValue("json", out Value? jsonValue))
            {
                continue;
            }

            NeuronExperience? entry = TryDeserializeExperience(jsonValue.StringValue);
            if (entry is not null)
            {
                results.Add(entry);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        // Attempt delete across all tiers — silently ignore "not found" errors.
        foreach (ExperienceTier tier in Enum.GetValues<ExperienceTier>())
        {
            string collection = TierCollectionNames.For(tier);
            try
            {
                await EnsureCollectionExistsAsync(collection, ct);
                await _client.DeleteAsync(collection, Guid.Parse(id), ct);
                _log?.Debug("Deleted entry {Id} from tier {Tier}", id, tier);
                return;
            }
            catch (Exception ex)
            {
                _log?.Debug("Entry {Id} not in tier {Tier} — {Error}", id, tier, ex.Message);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task EnsureCollectionExistsAsync(string collectionName, CancellationToken ct)
    {
        if (_ensuredCollections.Contains(collectionName))
        {
            return;
        }

        await _client.CreateCollectionAsync(
            collectionName,
            new VectorParams { Size = (ulong)_options.VectorSize, Distance = Distance.Cosine },
            ct);

        _log?.LogInformation("Created Qdrant collection {Collection}", collectionName);
        _ensuredCollections.Add(collectionName);
    }

    private static NeuronExperience? TryDeserializeExperience(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<NeuronExperience>(json);
        }
        catch
        {
            return null;
        }
    }
}
