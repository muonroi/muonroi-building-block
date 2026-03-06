using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.AspNetCore.Services;

internal sealed class NoopCatalogScanService : ICatalogScanService
{
    public Task<IReadOnlyList<MUiEngineCatalogApiDescriptor>> ScanApisAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MUiEngineCatalogApiDescriptor>>([]);
    }

    public Task<IReadOnlyList<MUiEngineCatalogRuleDescriptor>> ScanRulesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MUiEngineCatalogRuleDescriptor>>([]);
    }

    public Task<IReadOnlyList<MUiEngineCatalogBinding>> BuildBindingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MUiEngineCatalogBinding>>([]);
    }

    public Task<MUiEngineCatalogGraph> BuildGraphAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new MUiEngineCatalogGraph());
    }
}
