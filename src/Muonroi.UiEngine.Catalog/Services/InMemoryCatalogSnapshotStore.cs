namespace Muonroi.UiEngine.Catalog.Services;

internal sealed class InMemoryCatalogSnapshotStore(
    IMDateTimeService dateTimeService,
    IMJsonSerializeService jsonSerializeService) : ICatalogSnapshotStore
{
    private readonly ConcurrentDictionary<string, List<MUiEngineCatalogSnapshot>> _snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<MUiEngineCatalogSnapshot> SaveSnapshotAsync(
        string tenantId,
        MUiEngineCatalogGraph graph,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        MUiEngineCatalogSnapshot snapshot = CatalogSnapshotStoreHelper.CreateSnapshot(
            tenantId,
            graph,
            dateTimeService,
            jsonSerializeService);

        string normalizedTenantId = CatalogSnapshotStoreHelper.NormalizeTenantId(tenantId);
        _snapshots.AddOrUpdate(
            normalizedTenantId,
            _ => [CatalogSnapshotStoreHelper.CloneSnapshot(snapshot, jsonSerializeService)],
            (_, items) =>
            {
                lock (items)
                {
                    items.Add(CatalogSnapshotStoreHelper.CloneSnapshot(snapshot, jsonSerializeService));
                }

                return items;
            });

        return Task.FromResult(CatalogSnapshotStoreHelper.CloneSnapshot(snapshot, jsonSerializeService));
    }

    public Task<MUiEngineCatalogSnapshot?> GetLatestSnapshotAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedTenantId = CatalogSnapshotStoreHelper.NormalizeTenantId(tenantId);
        if (!_snapshots.TryGetValue(normalizedTenantId, out List<MUiEngineCatalogSnapshot>? items))
        {
            return Task.FromResult<MUiEngineCatalogSnapshot?>(null);
        }

        lock (items)
        {
            MUiEngineCatalogSnapshot? snapshot = items
                .OrderByDescending(item => item.CapturedAt)
                .ThenByDescending(item => item.SnapshotId)
                .Select(item => CatalogSnapshotStoreHelper.CloneSnapshot(item, jsonSerializeService))
                .FirstOrDefault();

            return Task.FromResult(snapshot);
        }
    }

    public Task<IReadOnlyList<CatalogSnapshotSummary>> ListSnapshotsAsync(
        string tenantId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedTenantId = CatalogSnapshotStoreHelper.NormalizeTenantId(tenantId);
        if (!_snapshots.TryGetValue(normalizedTenantId, out List<MUiEngineCatalogSnapshot>? items))
        {
            return Task.FromResult<IReadOnlyList<CatalogSnapshotSummary>>([]);
        }

        int normalizedLimit = Math.Clamp(limit, 1, 200);
        lock (items)
        {
            CatalogSnapshotSummary[] summaries = [.. items
                .OrderByDescending(item => item.CapturedAt)
                .ThenByDescending(item => item.SnapshotId)
                .Take(normalizedLimit)
                .Select(CatalogSnapshotStoreHelper.ToSummary)];

            return Task.FromResult<IReadOnlyList<CatalogSnapshotSummary>>(summaries);
        }
    }
}
