namespace Muonroi.UiEngine.Catalog.Services;

/// <summary>
/// Stores captured UI engine catalog snapshots per tenant.
/// </summary>
public interface ICatalogSnapshotStore
{
    /// <summary>
    /// Saves a new snapshot for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="graph">The graph payload to store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored snapshot.</returns>
    Task<MUiEngineCatalogSnapshot> SaveSnapshotAsync(
        string tenantId,
        MUiEngineCatalogGraph graph,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent snapshot for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The latest snapshot, if any.</returns>
    Task<MUiEngineCatalogSnapshot?> GetLatestSnapshotAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the most recent snapshots for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="limit">The maximum number of items to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot summaries.</returns>
    Task<IReadOnlyList<CatalogSnapshotSummary>> ListSnapshotsAsync(
        string tenantId,
        int limit = 20,
        CancellationToken cancellationToken = default);
}
