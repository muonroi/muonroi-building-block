using Microsoft.AspNetCore.Mvc;
using Muonroi.AspNetCore.Models.Changes;
using Muonroi.AspNetCore.Services;

namespace Quickstart.AspNetCore.RuleEngine.Api.Controllers;

/// <summary>
/// Exercises IRuleChangeStore (InMemoryRuleChangeStore) registered by
/// AddRuleEngineInfrastructure(). Rule ordering per (tenant, endpoint) can be
/// read, changed, rolled back, and audited via history.
/// </summary>
[ApiController]
[Route("api/rule-order")]
public sealed class RuleOrderController(IRuleChangeStore ruleChangeStore) : ControllerBase
{
    // GET api/rule-order?tenantId=acme&endpointRoute=/products
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(
        [FromQuery] string tenantId,
        [FromQuery] string endpointRoute,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> order =
            await ruleChangeStore.GetCurrentAsync(tenantId, endpointRoute, cancellationToken);
        return Ok(order);
    }

    // POST api/rule-order
    // Applies a new ordered list of rule codes for the (tenant, endpoint) pair.
    [HttpPost]
    [ProducesResponseType(typeof(RuleChangeRecord), StatusCodes.Status200OK)]
    public async Task<IActionResult> Apply(
        [FromBody] RuleOrderChangeRequest request,
        CancellationToken cancellationToken)
    {
        RuleChangeRecord record =
            await ruleChangeStore.ApplyAsync(request, appliedBy: "quickstart", cancellationToken);
        return Ok(record);
    }

    // GET api/rule-order/history?tenantId=acme&endpointRoute=/products
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<RuleChangeRecord>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string tenantId,
        [FromQuery] string endpointRoute,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RuleChangeRecord> history =
            await ruleChangeStore.GetHistoryAsync(tenantId, endpointRoute, cancellationToken);
        return Ok(history);
    }
}
