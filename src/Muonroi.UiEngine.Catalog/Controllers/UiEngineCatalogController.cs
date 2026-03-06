using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Muonroi.Tenancy.Core;
using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.UiEngine.Catalog.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ui-engine/catalog")]
public sealed class UiEngineCatalogController(
    ICatalogScanService catalogScanService,
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

    private Task<TResponse> GetOrCreateAsync<TResponse>(
        string suffix,
        Func<CancellationToken, Task<TResponse>> factory,
        CancellationToken cancellationToken)
    {
        string tenantId = string.IsNullOrWhiteSpace(TenantContext.CurrentTenantId)
            ? "_global"
            : TenantContext.CurrentTenantId;
        string key = $"ui-engine:catalog:{tenantId}:{suffix}";

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
        cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });

        return value;
    }
}
