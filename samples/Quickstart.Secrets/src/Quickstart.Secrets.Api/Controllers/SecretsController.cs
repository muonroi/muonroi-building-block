using Microsoft.AspNetCore.Mvc;
using Muonroi.Secrets.Secrets;

namespace Quickstart.Secrets.Api.Controllers;

/// <summary>
/// Demonstrates the Muonroi secrets surface.
/// Injects ISecretProvider and resolves named secrets by key.
/// </summary>
[ApiController]
[Route("api/secrets")]
public sealed class SecretsController(ISecretProvider secretProvider) : ControllerBase
{
    // GET api/secrets?name=Secrets:ApiKey
    // Resolves a secret by name. ConfigurationSecretProvider supports nested
    // configuration keys using the ':' separator (e.g. Secrets:ApiKey).
    // See src/Muonroi.Secrets/Secrets/ConfigurationSecretProvider.cs:16.
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSecret([FromQuery] string name = "Secrets:ApiKey")
    {
        string? value = secretProvider.GetSecret(name);
        return value is null
            ? NotFound(new { name, found = false })
            : Ok(new { name, value, found = true });
    }
}
