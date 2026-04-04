using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Muonroi.UiEngine.Catalog.Controllers;

/// <summary>
/// Exposes connector catalog for the flow designer UI.
/// Route: /api/v1/ui-engine/connectors/catalog
/// Separate route base from MRuleCatalogCompatController to avoid {code} template conflict.
/// AllowAnonymous: Rule Studio calls this endpoint without consumer-app auth tokens.
/// </summary>
/// <remarks>
/// Initializes the controller with a service provider.
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/v1/ui-engine/connectors")]
public class MConnectorCatalogController(IServiceProvider serviceProvider) : ControllerBase
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <summary>
    /// List available connector types for the flow designer Connector Type dropdown.
    /// </summary>
    [HttpGet("catalog")]
    public IActionResult ListConnectorCatalog()
    {
        // Resolve IConnectorRegistry optionally — not all consumer apps register connectors
        Type? registryType = typeof(Muonroi.Integration.Abstractions.IConnectorRegistry);
        object? registry = _serviceProvider.GetService(registryType);

        if (registry is null)
        {
            return Ok(Array.Empty<object>());
        }

        var listMethod = registryType.GetMethod("ListAvailable");
        if (listMethod is null)
        {
            return Ok(Array.Empty<object>());
        }

        object? result = listMethod.Invoke(registry, null);
        return Ok(result);
    }
}
