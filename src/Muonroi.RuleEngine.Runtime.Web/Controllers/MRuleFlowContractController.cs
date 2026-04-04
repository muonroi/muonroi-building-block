using Muonroi.RuleEngine.Runtime.Web.Models;
using Muonroi.RuleEngine.Runtime.Web.Services;

namespace Muonroi.RuleEngine.Runtime.Web.Controllers;

/// <summary>
/// Provides flow contract endpoints for the mu-rule-flow-designer component.
/// Route paths match what <c>MRuleFlowContractService</c> frontend expects.
/// Consumers can override <see cref="IMRuleFlowContractProvider"/> to customize behavior.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="MRuleFlowContractController"/>.
/// </remarks>
[ApiController]
[Route("api/v1/rule-engine")]
public class MRuleFlowContractController(IMRuleFlowContractProvider contractProvider) : ControllerBase
{
    private readonly IMRuleFlowContractProvider _contractProvider = contractProvider;

    /// <summary>
    /// Get the I/O contract for a rule by source type and code.
    /// Called by MRuleFlowContractService: GET {baseUrl}/rule-contracts/{sourceType}/{sourceCode}
    /// </summary>
    [HttpGet("rule-contracts/{sourceType}/{sourceCode}")]
    public async Task<ActionResult<MRuleFlowContractLookupResponse>> MGetRuleContract(
        string sourceType, string sourceCode, CancellationToken ct)
    {
        var result = await _contractProvider.MGetContractAsync(sourceType, sourceCode, ct);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <summary>
    /// Get the flow-level contract.
    /// Called by MRuleFlowContractService: GET {baseUrl}/flow-contracts/{flowCode}
    /// </summary>
    [HttpGet("flow-contracts/{flowCode}")]
    public async Task<ActionResult<MRuleFlowContractLookupResponse>> MGetFlowContract(
        string flowCode, CancellationToken ct)
    {
        var result = await _contractProvider.MGetFlowContractAsync(flowCode, ct);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <summary>
    /// Get node authoring contract.
    /// Called by MRuleFlowContractService: GET {baseUrl}/rule-flow/{flowCode}/nodes/{nodeId}/authoring-contract
    /// </summary>
    [HttpGet("rule-flow/{flowCode}/nodes/{nodeId}/authoring-contract")]
    public async Task<ActionResult<MRuleFlowNodeContractResponse>> MGetNodeContract(
        string flowCode, string nodeId, CancellationToken ct)
    {
        var result = await _contractProvider.MGetNodeAuthoringContractAsync(flowCode, nodeId, ct);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <summary>
    /// List all available rulesets/flows.
    /// Called by MRuleFlowContractService: GET {baseUrl}/rulesets
    /// </summary>
    [HttpGet("flow-summaries")]
    public async Task<ActionResult<IReadOnlyList<MRuleFlowSummary>>> MListFlowSummaries(
        CancellationToken ct)
    {
        var result = await _contractProvider.MListFlowsAsync(ct);
        return Ok(result);
    }
}
