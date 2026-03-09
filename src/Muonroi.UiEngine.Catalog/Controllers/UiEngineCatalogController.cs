using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Muonroi.Core.Abstractions.Context;
using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.UiEngine.Catalog.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ui-engine/catalog")]
[Route("api/v1/ui-catalog")]
public sealed class UiEngineCatalogController(
    ICatalogScanService catalogScanService,
    ICatalogSnapshotStore catalogSnapshotStore,
    ISystemExecutionContextAccessor executionContextAccessor,
    IMemoryCache cache) : ControllerBase
{
    [HttpGet("apis")]
    public Task<IReadOnlyList<MUiEngineCatalogApiDescriptor>> GetApis(CancellationToken cancellationToken)
    {
        return GetOrCreateAsync("apis", catalogScanService.ScanApisAsync, cancellationToken);
    }

    [HttpGet("rules")]
    public Task<IReadOnlyList<MUiEngineCatalogRuleDescriptor>> GetRules(CancellationToken cancellationToken)
    {
        return GetOrCreateAsync("rules", catalogScanService.ScanRulesAsync, cancellationToken);
    }

    [HttpGet("bindings")]
    public Task<IReadOnlyList<MUiEngineCatalogBinding>> GetBindings(CancellationToken cancellationToken)
    {
        return GetOrCreateAsync("bindings", catalogScanService.BuildBindingsAsync, cancellationToken);
    }

    [HttpGet("graph")]
    public Task<MUiEngineCatalogGraph> GetGraph(CancellationToken cancellationToken)
    {
        return GetOrCreateAsync("graph", catalogScanService.BuildGraphAsync, cancellationToken);
    }

    [HttpGet("snapshots")]
    public Task<IReadOnlyList<CatalogSnapshotSummary>> GetSnapshots(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return catalogSnapshotStore.ListSnapshotsAsync(ResolveTenantId(), limit, cancellationToken);
    }

    [HttpGet("snapshots/latest")]
    public async Task<ActionResult<MUiEngineCatalogSnapshot>> GetLatestSnapshot(CancellationToken cancellationToken = default)
    {
        MUiEngineCatalogSnapshot? snapshot = await catalogSnapshotStore.GetLatestSnapshotAsync(
            ResolveTenantId(),
            cancellationToken);
        if (snapshot is null)
        {
            return NotFound();
        }

        return Ok(snapshot);
    }

    [HttpPost("snapshots/capture")]
    public async Task<ActionResult<MUiEngineCatalogSnapshot>> CaptureSnapshot(CancellationToken cancellationToken = default)
    {
        MUiEngineCatalogGraph graph = await catalogScanService.BuildGraphAsync(cancellationToken);
        MUiEngineCatalogSnapshot snapshot = await catalogSnapshotStore.SaveSnapshotAsync(
            ResolveTenantId(),
            graph,
            cancellationToken);

        cache.Set(BuildCacheKey("graph"), graph, BuildCacheOptions());
        return Ok(snapshot);
    }

    private Task<TResponse> GetOrCreateAsync<TResponse>(
        string suffix,
        Func<CancellationToken, Task<TResponse>> factory,
        CancellationToken cancellationToken)
    {
        string key = BuildCacheKey(suffix);

        if (cache.TryGetValue(key, out TResponse? existing) && existing is not null)
        {
            return Task.FromResult(existing);
        }

        return BuildAndCacheAsync(key, factory, cancellationToken);
    }

    private async Task<TResponse> BuildAndCacheAsync<TResponse>(
        string key,
        Func<CancellationToken, Task<TResponse>> factory,
        CancellationToken cancellationToken)
    {
        TResponse value = await factory(cancellationToken);
        cache.Set(key, value, BuildCacheOptions());

        return value;
    }

    private string BuildCacheKey(string suffix)
    {
        return $"ui-engine:catalog:{ResolveTenantId()}:{suffix}";
    }

    private string ResolveTenantId()
    {
        string? tenantId = executionContextAccessor.Get().TenantId;
        return string.IsNullOrWhiteSpace(tenantId) ? "_global" : tenantId.Trim();
    }

    private static MemoryCacheEntryOptions BuildCacheOptions()
    {
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
    }
}
