namespace Quickstart.Grpc.Api.Controllers;

/// <summary>
/// REST facade that invokes the BaseGrpcService-derived <see cref="GreeterClientService"/>,
/// showing how the resilient gRPC call pipeline is consumed by application code.
/// </summary>
[ApiController]
[Route("api/greeter")]
public sealed class GreeterController(GreeterClientService greeter) : ControllerBase
{
    // GET api/greeter?name=World
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SayHello([FromQuery] string name = "World")
    {
        string message = await greeter.SayHelloAsync(name);
        return Ok(new { message });
    }
}
