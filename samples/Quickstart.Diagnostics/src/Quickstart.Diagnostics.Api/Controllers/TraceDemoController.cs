using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Diagnostics;

namespace Quickstart.Diagnostics.Api.Controllers;

/// <summary>
/// Demonstrates the Muonroi diagnostics trace surface.
/// Injects IMTraceContext to open a session, build a hierarchical node tree,
/// record events, and export the captured trace.
/// </summary>
[ApiController]
[Route("api/trace-demo")]
public sealed class TraceDemoController(IMTraceContext traceContext) : ControllerBase
{
    // POST api/trace-demo/run?sessionId=abc
    // Opens a trace session, nests two child nodes, records events on each,
    // then exports the full trace tree as JSON.
    // See src/Muonroi.Core.Abstractions/Diagnostics/IMTraceContext.cs:17 (Begin)
    // and ITraceSession.cs:39 (BeginNode) / :42 (Record) / :57 (Export).
    [HttpPost("run")]
    [ProducesResponseType(typeof(MTraceSessionRecord), StatusCodes.Status200OK)]
    public IActionResult Run(
        [FromQuery] string sessionId = "quickstart-session",
        [FromQuery] string? tenantId = "tenant-1",
        [FromQuery] string? userId = "user-1")
    {
        // Begin() returns an IDisposable scope that restores the previous session on dispose.
        using IDisposable session = traceContext.Begin(sessionId, tenantId, userId);

        ITraceSession active = traceContext.Current!;

        using (active.BeginNode("LoadOrder", MTraceNodeType.Handler))
        {
            active.Record("Loaded order from store", new { orderId = 42 });

            using (active.BeginNode("ValidateOrder", MTraceNodeType.Rule))
            {
                active.Record("Validation passed");
            }
        }

        // Export the captured trace tree (nodes, events, durations).
        MTraceSessionRecord record = active.Export();
        return Ok(record);
    }
}
