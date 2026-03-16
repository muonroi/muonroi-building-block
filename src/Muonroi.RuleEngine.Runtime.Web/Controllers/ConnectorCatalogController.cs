using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Integration.Abstractions;

namespace Muonroi.RuleEngine.Runtime.Web.Controllers;

/// <summary>
/// Exposes connector catalog for the flow designer UI.
/// Consumer apps that register AddMBuiltInConnectors() automatically get this endpoint.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/rule-engine/connectors")]
public sealed class ConnectorCatalogController(IConnectorRegistry registry) : ControllerBase
{
    [HttpGet("catalog")]
    public IActionResult ListCatalog()
    {
        IReadOnlyList<ConnectorMetadata> available = registry.ListAvailable();

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
