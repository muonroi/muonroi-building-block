using Microsoft.AspNetCore.Mvc;
using Muonroi.Integration.Abstractions;

namespace Muonroi.UiEngine.Catalog.Controllers;

/// <summary>
/// Exposes connector catalog for the flow designer UI.
/// Registered alongside <see cref="MRuleCatalogCompatController"/> via AddUiEngineCatalog().
/// </summary>
[ApiController]
[Route("api/v1/ui-engine/catalog")]
public class MConnectorCatalogController : ControllerBase
{
    private readonly IConnectorRegistry? _registry;

    /// <summary>
    /// Constructor. IConnectorRegistry is optional — if not registered, returns empty catalog.
    /// </summary>
    public MConnectorCatalogController(IServiceProvider serviceProvider)
    {
        _registry = serviceProvider.GetService(typeof(IConnectorRegistry)) as IConnectorRegistry;
    }

    /// <summary>
    /// List available connector types for the flow designer Connector Type dropdown.
    /// </summary>
    [HttpGet("connectors")]
    public IActionResult ListConnectorCatalog()
    {
        if (_registry is null)
        {
            return Ok(Array.Empty<object>());
        }

        IReadOnlyList<ConnectorMetadata> available = _registry.ListAvailable();

        var catalog = available.Select(m => new
        {
            m.Type,
            m.DisplayName,
            m.Category,
            m.Description,
            m.RequiresCredentials,
            m.IconSvg
        });

        return Ok(catalog);
    }
}
