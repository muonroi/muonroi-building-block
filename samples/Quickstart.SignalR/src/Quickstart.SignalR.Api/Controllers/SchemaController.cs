using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Models;
using Muonroi.SignalR.SignalR;

namespace Quickstart.SignalR.Api.Controllers;

/// <summary>
/// Triggers a real-time schema-change broadcast through IUiEngineSchemaNotifier.
/// Connected SignalR clients that called SubscribeToSchemaChanges() on
/// <see cref="MUiEngineHub"/> receive a "SchemaChanged" event.
/// </summary>
[ApiController]
[Route("api/schema")]
public sealed class SchemaController(IUiEngineSchemaNotifier notifier) : ControllerBase
{
    // POST api/schema/notify?hash=abc123
    // Broadcasts a schema-change notification to all watchers.
    [HttpPost("notify")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Notify(
        [FromQuery] string hash,
        CancellationToken cancellationToken)
    {
        MUiEngineSchemaVersion version = new()
        {
            SchemaHash = hash,
            GeneratedAtUtc = DateTime.UtcNow
        };

        await notifier.NotifySchemaChangedAsync(version, cancellationToken);
        return Accepted(new { broadcast = true, schemaHash = hash });
    }
}
