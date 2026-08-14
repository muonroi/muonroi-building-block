namespace Muonroi.Experience.Runtime.Archive;

/// <summary>
/// <see cref="IExperienceArchive"/> backed by a dedicated Qdrant collection named
/// <c>experience_archive</c>. Mirrors the <see cref="QdrantExperienceStore"/> UpsertAsync
/// pattern — stores serialised JSON in the <c>json</c> payload key without budget checks.
/// </summary>
public sealed class QdrantExperienceArchive : IExperienceArchive
{
    private const string ArchiveCollection = "experience_archive";

    private readonly IQdrantClientWrapper _client;
    private readonly int _vectorSize;
    private readonly IMLog<QdrantExperienceArchive>? _log;
    private bool _collectionEnsured;

    /// <summary>
    /// Initialises a new <see cref="QdrantExperienceArchive"/>.
    /// </summary>
    public QdrantExperienceArchive(
        IQdrantClientWrapper client,
        IOptions<ExperienceStoreOptions> options,
        IMLog<QdrantExperienceArchive>? log = null)
    {
        _client = MGuard.NotNull(client);
        _vectorSize = MGuard.NotNull(options).Value.VectorSize;
        _log = log;

        MGuard.Against(_vectorSize <= 0, "VectorSize must be > 0");
    }

    /// <inheritdoc/>
    public async Task ArchiveAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        await EnsureCollectionAsync(ct);

        var point = new PointStruct
        {
            Id = new PointId { Uuid = experience.Id },
            Vectors = new float[_vectorSize]
        };
        point.Payload["json"] = JsonSerializer.Serialize(experience);

        await _client.UpsertAsync(ArchiveCollection, [point], ct);
        _log?.Debug("Archived entry {Id} to Qdrant archive collection", experience.Id);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<NeuronExperience>> GetArchivedAsync(CancellationToken ct = default)
    {
        await EnsureCollectionAsync(ct);

        IReadOnlyList<RetrievedPoint> points = await _client.ScrollAsync(ArchiveCollection, limit: 1000, ct);

        var results = new List<NeuronExperience>();
        foreach (RetrievedPoint point in points)
        {
            if (!point.Payload.TryGetValue("json", out Value? jsonValue))
                continue;

            NeuronExperience? entry = TryDeserialize(jsonValue.StringValue);
            if (entry is not null)
                results.Add(entry);
        }

        return results;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        if (_collectionEnsured) return;

        await _client.CreateCollectionAsync(
            ArchiveCollection,
            new VectorParams { Size = (ulong)_vectorSize, Distance = Distance.Cosine },
            ct);

        _collectionEnsured = true;
    }

    private static NeuronExperience? TryDeserialize(string json)
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
