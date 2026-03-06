using Muonroi.UiEngine.Catalog.Models;

namespace Muonroi.UiEngine.Catalog.Services;

public interface ICatalogScanService
{
    Task<IReadOnlyList<MUiEngineCatalogApiDescriptor>> ScanApisAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MUiEngineCatalogRuleDescriptor>> ScanRulesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MUiEngineCatalogBinding>> BuildBindingsAsync(CancellationToken cancellationToken = default);

    Task<MUiEngineCatalogGraph> BuildGraphAsync(CancellationToken cancellationToken = default);
}
