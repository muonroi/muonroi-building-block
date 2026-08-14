namespace Quickstart.Logging.Api.Controllers;

/// <summary>
/// Demonstrates the Muonroi structured logging surface.
/// Injects IMLog&lt;T&gt; (per-category logger) and IMLogFactory (logger factory).
/// </summary>
[ApiController]
[Route("api/log-demo")]
public sealed class LogDemoController(
    IMLog<LogDemoController> log,
    IMLogFactory logFactory) : ControllerBase
{
    // POST api/log-demo/emit?message=hello
    // Emits one log line at each level via the IMLog<T> helper methods.
    // See src/Muonroi.Logging.Abstractions/IMLog.cs:21 (Info/Warn/Error/Debug).
    [HttpPost("emit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Emit([FromQuery] string message = "hello from quickstart")
    {
        log.Debug("Debug: {Message}", message);
        log.Info("Info: {Message}", message);
        log.Warn("Warn: {Message}", message);
        log.Error(new InvalidOperationException("demo exception"), "Error: {Message}", message);

        return Ok(new { emitted = new[] { "Debug", "Info", "Warn", "Error" }, message });
    }

    // POST api/log-demo/scoped?orderId=42
    // Pushes an ambient property via IMLog.BeginProperty (backed by IMLogContext.PushProperty).
    // Every log line emitted inside the using-scope carries the property.
    // See src/Muonroi.Logging.Abstractions/IMLog.cs:14 and IMLogContext.cs:14.
    [HttpPost("scoped")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Scoped([FromQuery] int orderId = 42)
    {
        using IMLogContextScope scope = log.BeginProperty("OrderId", orderId);
        log.Info("Processing order inside scoped property");
        return Ok(new { scopedProperty = "OrderId", value = orderId });
    }

    // POST api/log-demo/factory?category=Payments
    // Creates a logger by category name via IMLogFactory.CreateLogger(string).
    // See src/Muonroi.Logging/MLogFactory.cs:35.
    [HttpPost("factory")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Factory([FromQuery] string category = "Payments")
    {
        IMLog categoryLog = logFactory.CreateLogger(category);
        categoryLog.Info("Log line emitted from factory-created logger for category {Category}", category);
        return Ok(new { createdLoggerCategory = category });
    }
}
