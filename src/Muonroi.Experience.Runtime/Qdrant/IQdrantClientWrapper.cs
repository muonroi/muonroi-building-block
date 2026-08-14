namespace Muonroi.Experience.Runtime.Qdrant;

/// <summary>
/// Mockable wrapper interface for QdrantClient.
/// Wraps the four QdrantClient methods used by QdrantExperienceStore so that
/// production code never depends on the concrete (non-virtual) QdrantClient directly.
/// </summary>
public interface IQdrantClientWrapper
{
    /// <summary>Creates a Qdrant collection with the given vector parameters. Idempotent — already-exists errors are suppressed.</summary>
    Task CreateCollectionAsync(string collectionName, VectorParams vectorParams, CancellationToken ct = default);

    /// <summary>Upserts a list of points into the named collection.</summary>
    Task UpsertAsync(string collectionName, IReadOnlyList<PointStruct> points, CancellationToken ct = default);

    /// <summary>
    /// Performs a vector similarity search in the named collection.
    /// Returns the top-<paramref name="limit"/> scored points ordered by descending score.
    /// </summary>
    Task<IReadOnlyList<ScoredPoint>> SearchAsync(string collectionName, float[] queryVector, ulong limit, CancellationToken ct = default);

    /// <summary>
    /// Scrolls (scans) all points in the named collection up to <paramref name="limit"/>.
    /// Returns the retrieved points; the next-offset token is discarded (full scan assumed).
    /// </summary>
    Task<IReadOnlyList<RetrievedPoint>> ScrollAsync(string collectionName, uint limit, CancellationToken ct = default);

    /// <summary>Deletes a single point by its GUID identifier.</summary>
    Task DeleteAsync(string collectionName, Guid pointId, CancellationToken ct = default);
}
