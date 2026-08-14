namespace Quickstart.Observability.Api.Controllers;

/// <summary>
/// Lightweight health endpoint.
/// Returns the service name and assembly version so that callers (and the
/// OpenTelemetry <c>service.version</c> resource attribute) can be verified
/// without hitting any business logic.
/// </summary>
[ApiController]
[Route("[controller]")]
public sealed class HealthController : ControllerBase
{
    private static readonly string ServiceName =
        typeof(HealthController).Assembly.GetName().Name ?? "Quickstart.Observability";

    private static readonly string Version =
        typeof(HealthController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? typeof(HealthController).Assembly.GetName().Version?.ToString()
        ?? "1.0.0";

    /// <summary>
    /// Returns the service name, version, and current UTC time.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            status      = "Healthy",
            service     = ServiceName,
            version     = Version,
            utcTime     = DateTime.UtcNow
        });
    }
}
