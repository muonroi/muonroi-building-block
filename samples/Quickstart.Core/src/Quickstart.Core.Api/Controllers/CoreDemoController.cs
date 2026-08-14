namespace Quickstart.Core.Api.Controllers;

/// <summary>
/// Demonstrates the foundational Muonroi.Core services registered by AddCoreServices().
/// </summary>
[ApiController]
[Route("api/core-demo")]
public sealed class CoreDemoController(
    IMDateTimeService dateTime,
    IMJsonSerializeService json,
    ISystemExecutionContextAccessor contextAccessor) : ControllerBase
{
    // GET api/core-demo/now
    // Returns the current time from IMDateTimeService (testable clock abstraction).
    // See src/Muonroi.Core/Helpers/MDateTimeService.cs:8 and
    // src/Muonroi.Core.Abstractions/Interfaces/IMDateTimeService.cs:6.
    [HttpGet("now")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Now()
    {
        return Ok(new
        {
            now = dateTime.Now(),
            utcNow = dateTime.UtcNow(),
            today = dateTime.Today(),
            utcToday = dateTime.UtcToday(),
            nowTs = dateTime.NowTs(),
            utcNowTs = dateTime.UtcNowTs()
        });
    }

    // POST api/core-demo/json-roundtrip
    // Serializes the payload to a string then deserializes it back via IMJsonSerializeService.
    // See src/Muonroi.Core.Abstractions/SeedWorks/MJsonSerializeService.cs:10 and
    // src/Muonroi.Core.Abstractions/Interfaces/IMJsonSerializeService.cs:6.
    [HttpPost("json-roundtrip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult JsonRoundtrip([FromBody] SamplePayload payload)
    {
        string serialized = json.Serialize(payload);
        SamplePayload? roundTripped = json.Deserialize<SamplePayload>(serialized);
        return Ok(new { serialized, roundTripped });
    }

    // GET api/core-demo/context
    // Sets an ambient execution context, reads it back through the accessor, then clears it.
    // See src/Muonroi.Core.Abstractions/Context/ISystemExecutionContextAccessor.cs:9.
    [HttpGet("context")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Context()
    {
        ISystemExecutionContext ctx = new SystemExecutionContext(
            tenantId: "tenant-1",
            userId: "user-42",
            username: "quickstart",
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null,
            apiKey: null,
            isAuthenticated: true,
            permissions: ["orders:read"],
            sourceType: "http");

        contextAccessor.Set(ctx);
        ISystemExecutionContext current = contextAccessor.Get();
        contextAccessor.Clear();

        return Ok(new
        {
            current.TenantId,
            current.UserId,
            current.Username,
            current.CorrelationId,
            current.IsAuthenticated,
            current.Permissions
        });
    }
}

/// <summary>Sample payload for the JSON round-trip demo.</summary>
public sealed record SamplePayload(string Name, int Quantity, decimal Price);
