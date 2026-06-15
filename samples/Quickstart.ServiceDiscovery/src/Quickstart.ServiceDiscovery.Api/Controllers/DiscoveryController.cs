using Microsoft.AspNetCore.Mvc;
using Muonroi.ServiceDiscovery.Consul.Consul;

namespace Quickstart.ServiceDiscovery.Api.Controllers;

/// <summary>
/// Exposes the bound <see cref="ConsulConfigs"/> registered by
/// <c>AddServiceDiscovery</c>, and reports whether a live Consul client was wired.
/// </summary>
[ApiController]
[Route("api/discovery")]
public class DiscoveryController(
    ConsulConfigs consulConfigs,
    IServiceProvider services,
    IWebHostEnvironment environment) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // 1. Read the bound Consul configuration
    //    GET /api/discovery/config
    //
    //    AddServiceDiscovery always registers ConsulConfigs as a singleton (bound from
    //    the "ConsulConfigs" section), even when discovery itself is disabled.
    // ---------------------------------------------------------------------------
    [HttpGet("config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            sectionName = ConsulConfigs.SectionName, // "ConsulConfigs"
            consulConfigs.Enable,
            consulConfigs.UseDiscovery,
            consulConfigs.ServiceName,
            consulConfigs.ConsulAddress,
            consulConfigs.ServiceAddress,
            consulConfigs.ServicePort,
            consulConfigs.ServiceMetadata
        });
    }

    // ---------------------------------------------------------------------------
    // 2. Report whether a live Consul client was registered
    //    GET /api/discovery/status
    //
    //    IConsulClient is only registered when discovery is enabled, the environment is
    //    NOT Development, and ServiceName + ConsulAddress are configured. The absence of
    //    IConsulClient in DI is the package's signal that discovery is disabled.
    // ---------------------------------------------------------------------------
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        // Avoid a hard compile dependency on the Consul client type — probe DI by name.
        object? consulClient = services.GetService(typeof(global::Consul.IConsulClient));

        return Ok(new
        {
            environment = environment.EnvironmentName,
            isDevelopment = environment.IsDevelopment(),
            consulClientRegistered = consulClient is not null,
            note = environment.IsDevelopment()
                ? "Development environment: AddServiceDiscovery short-circuits and no IConsulClient is registered."
                : "Set ConsulConfigs:ServiceName + ConsulConfigs:ConsulAddress to register with Consul."
        });
    }
}
