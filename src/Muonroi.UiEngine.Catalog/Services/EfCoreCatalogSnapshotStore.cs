using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Persistence;

namespace Muonroi.UiEngine.Catalog.Services;

internal sealed class EfCoreCatalogSnapshotStore(
    UiEngineCatalogDbContext dbContext,
    IMDateTimeService dateTimeService,
    IMJsonSerializeService jsonSerializeService) : ICatalogSnapshotStore
{
    public async Task<MUiEngineCatalogSnapshot> SaveSnapshotAsync(
        string tenantId,
        MUiEngineCatalogGraph graph,
        CancellationToken cancellationToken = default)
    {
        MUiEngineCatalogSnapshot snapshot = CatalogSnapshotStoreHelper.CreateSnapshot(
            tenantId,
            graph,
            dateTimeService,
            jsonSerializeService);

        CatalogSnapshotEntity entity = new()
        {
            SnapshotId = snapshot.SnapshotId,
            TenantId = snapshot.TenantId,
            CapturedAt = snapshot.CapturedAt,
            GraphJson = jsonSerializeService.Serialize(snapshot.Graph),
            ApiCount = snapshot.ApiCount,
            RuleCount = snapshot.RuleCount,
            BindingCount = snapshot.BindingCount
        };

        dbContext.Snapshots.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CatalogSnapshotStoreHelper.CloneSnapshot(snapshot, jsonSerializeService);
    }

    public async Task<MUiEngineCatalogSnapshot?> GetLatestSnapshotAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        string normalizedTenantId = CatalogSnapshotStoreHelper.NormalizeTenantId(tenantId);
        CatalogSnapshotEntity? entity = await dbContext.Snapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.TenantId == normalizedTenantId)
            .OrderByDescending(snapshot => snapshot.CapturedAt)
            .ThenByDescending(snapshot => snapshot.SnapshotId)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<CatalogSnapshotSummary>> ListSnapshotsAsync(
        string tenantId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        string normalizedTenantId = CatalogSnapshotStoreHelper.NormalizeTenantId(tenantId);
        int normalizedLimit = Math.Clamp(limit, 1, 200);

        CatalogSnapshotSummary[] summaries = [.. await dbContext.Snapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.TenantId == normalizedTenantId)
            .OrderByDescending(snapshot => snapshot.CapturedAt)
            .ThenByDescending(snapshot => snapshot.SnapshotId)
            .Take(normalizedLimit)
            .Select(snapshot => new CatalogSnapshotSummary
            {
                SnapshotId = snapshot.SnapshotId,
                TenantId = snapshot.TenantId,
                CapturedAt = snapshot.CapturedAt,
                ApiCount = snapshot.ApiCount,
                RuleCount = snapshot.RuleCount,
                BindingCount = snapshot.BindingCount
            })
            .ToListAsync(cancellationToken)];

        return summaries;
    }

    private MUiEngineCatalogSnapshot? Map(CatalogSnapshotEntity entity)
    {
        MUiEngineCatalogGraph? graph = jsonSerializeService.Deserialize<MUiEngineCatalogGraph>(entity.GraphJson);
        if (graph is null)
        {
            return null;
        }

        return new MUiEngineCatalogSnapshot
        {
            SnapshotId = entity.SnapshotId,
            TenantId = entity.TenantId,
            CapturedAt = entity.CapturedAt,
            Graph = graph,
            ApiCount = entity.ApiCount,
            RuleCount = entity.RuleCount,
            BindingCount = entity.BindingCount
        };
    }
}
