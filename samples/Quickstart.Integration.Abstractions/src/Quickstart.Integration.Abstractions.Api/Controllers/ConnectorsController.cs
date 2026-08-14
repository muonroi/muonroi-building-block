using Microsoft.AspNetCore.Mvc;
using Muonroi.Integration.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using System.Text.Json;

namespace Quickstart.Integration.Abstractions.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConnectorsController : ControllerBase
{
    private readonly IConnectorRegistry _registry;

    public ConnectorsController(IConnectorRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public IActionResult ListAvailable()
    {
        return Ok(_registry.ListAvailable());
    }

    [HttpPost("{connectorType}/execute")]
    public async Task<IActionResult> ExecuteConnector(string connectorType, [FromBody] Dictionary<string, object> inputData)
    {
        var connector = _registry.Resolve(connectorType);
        if (connector == null)
            return NotFound($"Connector '{connectorType}' not found.");

        var context = new ConnectorContext
        {
            Config = JsonDocument.Parse("{}"),
            InputFacts = new FactBag(inputData),
            Credentials = new Dictionary<string, string> { { "ApiKey", "mock-key" } },
            TenantId = "tenant-1",
            CorrelationId = Guid.NewGuid().ToString()
        };

        var result = await connector.ExecuteAsync(context, HttpContext.RequestAborted);
        return Ok(result);
    }
}
