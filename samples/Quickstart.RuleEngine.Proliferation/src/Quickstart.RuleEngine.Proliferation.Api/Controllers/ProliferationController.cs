using Microsoft.AspNetCore.Mvc;
using Muonroi.RuleEngine.Proliferation;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Quickstart.RuleEngine.Proliferation.Api.Controllers;

/// <summary>
/// Demonstrates the IProliferationStore primary API (backed by Postgres).
///
/// These endpoints read scenario / result / stats data produced by the
/// proliferation engine. They require a reachable Postgres instance configured
/// via the ProliferationDb connection string.
/// </summary>
[ApiController]
[Route("api/proliferation")]
public sealed class ProliferationController(IProliferationStore store) : ControllerBase
{
    /// <summary>Returns aggregate proliferation statistics, optionally per seed rule.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] string? seedRuleCode, CancellationToken ct)
    {
        ProliferationStats stats = await store.GetStatsAsync(seedRuleCode, ct);
        return Ok(stats);
    }

    /// <summary>Lists scenarios that are still pending execution.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        IReadOnlyList<NeuronScenario> pending = await store.GetPendingScenariosAsync(limit, ct);
        return Ok(pending);
    }

    /// <summary>Returns the rule lineage (proliferation graph) for a seed rule.</summary>
    [HttpGet("lineage/{seedRuleCode}")]
    public async Task<IActionResult> GetLineage(string seedRuleCode, CancellationToken ct)
    {
        IReadOnlyList<RuleLineage> lineage = await store.GetLineageAsync(seedRuleCode, ct);
        return Ok(lineage);
    }
}
