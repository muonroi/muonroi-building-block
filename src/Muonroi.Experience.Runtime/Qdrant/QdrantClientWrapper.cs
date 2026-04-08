using Grpc.Core;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using QdrantGrpc = Qdrant.Client.Grpc;

namespace Muonroi.Experience.Runtime.Qdrant;

/// <summary>
/// Production implementation of <see cref="IQdrantClientWrapper"/> that delegates to a real <see cref="QdrantClient"/>.
/// </summary>
public sealed class QdrantClientWrapper : IQdrantClientWrapper
{
    private readonly QdrantClient _client;

    /// <summary>Initialises a new wrapper around the supplied <see cref="QdrantClient"/> instance.</summary>
    public QdrantClientWrapper(QdrantClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public async Task CreateCollectionAsync(string collectionName, VectorParams vectorParams, CancellationToken ct = default)
    {
        try
        {
            await _client.CreateCollectionAsync(collectionName, vectorParams, cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Idempotent — collection already exists; nothing to do.
        }
    }

    /// <inheritdoc />
    public async Task UpsertAsync(string collectionName, IReadOnlyList<PointStruct> points, CancellationToken ct = default)
    {
        await _client.UpsertAsync(collectionName, points, cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(string collectionName, float[] queryVector, ulong limit, CancellationToken ct = default)
    {
        return await _client.SearchAsync(collectionName, queryVector, limit: limit, cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetrievedPoint>> ScrollAsync(string collectionName, uint limit, CancellationToken ct = default)
    {
        QdrantGrpc.ScrollResponse response = await _client.ScrollAsync(collectionName, limit: limit, cancellationToken: ct);
        return response.Result;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collectionName, Guid pointId, CancellationToken ct = default)
    {
        await _client.DeleteAsync(collectionName, pointId, cancellationToken: ct);
    }
}
