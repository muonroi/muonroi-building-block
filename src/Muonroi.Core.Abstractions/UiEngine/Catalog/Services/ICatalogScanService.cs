using Muonroi.UiEngine.Catalog.Models;

namespace Muonroi.UiEngine.Catalog.Services;

/// <summary>
/// Service for scanning catalog items.
/// </summary>
public interface ICatalogScanService
{
    /// <summary>
    /// Scans for APIs.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of API descriptors.</returns>
    Task<IReadOnlyList<MUiEngineCatalogApiDescriptor>> ScanApisAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans for rules.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of rule descriptors.</returns>
    Task<IReadOnlyList<MUiEngineCatalogRuleDescriptor>> ScanRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds bindings.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of catalog bindings.</returns>
    Task<IReadOnlyList<MUiEngineCatalogBinding>> BuildBindingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a catalog graph.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The catalog graph.</returns>
    Task<MUiEngineCatalogGraph> BuildGraphAsync(CancellationToken cancellationToken = default);
}
