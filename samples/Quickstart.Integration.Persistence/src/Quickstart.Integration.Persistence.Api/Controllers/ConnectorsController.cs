






namespace Quickstart.Integration.Persistence.Api.Controllers;

/// <summary>
/// Demonstrates the connector config + credential stores backed by EF Core.
///
/// All endpoints require a reachable Postgres instance (ConnectorDb connection
/// string). Credentials are encrypted at rest by EfConnectorCredentialStore.
/// </summary>
[ApiController]
[Route("api/connectors")]
public sealed class ConnectorsController(
    IConnectorConfigStore configStore,
    IConnectorCredentialStore credentialStore) : ControllerBase
{
    // ---- Connector configurations -----------------------------------------

    /// <summary>Lists connector configurations for a tenant (null = global).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? tenantId, CancellationToken ct)
    {
        IReadOnlyList<ConnectorConfigDto> configs = await configStore.ListAsync(tenantId, null, ct);
        return Ok(configs);
    }

    /// <summary>Gets a single connector configuration by id.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, [FromQuery] string? tenantId, CancellationToken ct)
    {
        ConnectorConfigDto? config = await configStore.GetByIdAsync(id, tenantId, ct);
        return config is null ? NotFound() : Ok(config);
    }

    /// <summary>Creates or updates a connector configuration.</summary>
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ConnectorConfigDto config, CancellationToken ct)
    {
        ConnectorConfigDto saved = await configStore.SaveAsync(config, ct);
        return Ok(saved);
    }

    /// <summary>Deletes a connector configuration.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string? tenantId, CancellationToken ct)
    {
        await configStore.DeleteAsync(id, tenantId, ct);
        return NoContent();
    }

    // ---- Encrypted credentials --------------------------------------------

    /// <summary>Stores encrypted credential key/value pairs for a connector.</summary>
    [HttpPut("credentials/{credentialId}")]
    public async Task<IActionResult> SaveCredentials(
        string credentialId,
        [FromQuery] string? tenantId,
        [FromBody] Dictionary<string, string> values,
        CancellationToken ct)
    {
        await credentialStore.SaveAsync(credentialId, tenantId, values, ct);
        return Ok(new { credentialId, keys = values.Keys });
    }

    /// <summary>Reads and decrypts stored credentials for a connector.</summary>
    [HttpGet("credentials/{credentialId}")]
    public async Task<IActionResult> GetCredentials(
        string credentialId,
        [FromQuery] string? tenantId,
        CancellationToken ct)
    {
        IReadOnlyDictionary<string, string> values = await credentialStore.GetAsync(credentialId, tenantId, ct);
        return Ok(values);
    }
}
