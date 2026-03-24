using Muonroi.RuleEngine.Runtime.Web.Services;
using System.Diagnostics;

namespace Muonroi.RuleEngine.Runtime.Web.Controllers;

/// <summary>
/// Base controller providing POST /api/v1/rule-engine/execute/{workflowCode} for dry-run execution.
/// Consumer projects inherit this controller to expose the endpoint automatically.
/// Override <see cref="ExecuteAsync"/> to customize execution behavior.
/// </summary>
/// <param name="rulesEngineService">Rules engine service used to load rulesets.</param>
/// <param name="dryRunService">Dry-run executor for rule evaluation.</param>
/// <param name="executionContextAccessor">Execution context accessor for tenant resolution.</param>
[ApiController]
[Route("api/v1/rule-engine")]
public abstract class MRuleFlowExecuteController(
    RulesEngineService rulesEngineService,
    IRuleDryRunService dryRunService,
    ISystemExecutionContextAccessor executionContextAccessor) : ControllerBase
{
    /// <summary>Executes a ruleset dry-run with the provided input facts.</summary>
    /// <param name="workflowCode">Workflow identifier to execute.</param>
    /// <param name="version">Optional ruleset version.</param>
    /// <param name="inputFacts">Input facts for the ruleset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An action result containing dry-run output.</returns>
    [HttpPost("execute/{workflowCode}")]
    public virtual async Task<IActionResult> ExecuteAsync(
        string workflowCode,
        [FromQuery] int? version,
        [FromBody] JsonElement inputFacts,
        CancellationToken cancellationToken = default)
    {
        // 1. Load ruleset JSON from store
        string? ruleSetJson = await rulesEngineService.GetRuleSetAsync(workflowCode, version, cancellationToken);
        if (ruleSetJson is null)
        {
            return NotFound(new { message = $"Ruleset not found for workflow '{workflowCode}'." });
        }

        // 2. Parse input facts
        Dictionary<string, object?> facts = new(StringComparer.OrdinalIgnoreCase);
        if (inputFacts.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty prop in inputFacts.EnumerateObject())
            {
                facts[prop.Name] = NormalizeJsonElement(prop.Value);
            }
        }

        // 3. Execute dry-run
        string? tenantId = executionContextAccessor.Get().TenantId;
        Stopwatch sw = Stopwatch.StartNew();
        RuleDryRunResult result = await dryRunService.RunAsync(
            ruleSetJson,
            RuleSetFormat.Json,
            facts,
            tenantId,
            cancellationToken);
        sw.Stop();

        // 4. Map to FE-expected MDryRunResult shape
        var response = new
        {
            isSuccess = result.RulesMatched && result.Errors.Count == 0,
            errors = result.Errors,
            results = result.Traces.Select(t => new
            {
                ruleName = t.RuleName,
                isSuccess = t.Matched,
                evaluationResult = t.Matched ? "passed" : "failed",
                errorMessage = t.FailReason
            }),
            factBag = result.OutputFacts,
            executionTimeMs = (long)sw.Elapsed.TotalMilliseconds
        };

        return Ok(response);
    }

    /// <summary>
    /// Maps an <see cref="OrchestratorResult"/> to the FE-expected MDryRunResult response shape.
    /// Consumer overrides of <see cref="ExecuteAsync"/> should call this to return consistent dry-run responses.
    /// </summary>
    /// <param name="result">The orchestrator result from real pipeline execution.</param>
    /// <param name="elapsedMs">Execution time in milliseconds.</param>
    /// <returns>An anonymous object matching the MDryRunResult TypeScript interface.</returns>
    protected static object MapOrchestratorToMDryRunResponse(OrchestratorResult result, long elapsedMs)
    {
        return new
        {
            isSuccess = result.IsSuccess,
            errors = result.Errors,
            results = result.RuleResults.Select(kvp => new
            {
                ruleName = kvp.Key,
                isSuccess = kvp.Value.IsSuccess,
                evaluationResult = kvp.Value.IsSuccess,
                errors = kvp.Value.Errors
            }),
            factBag = result.Facts.AsReadOnly()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase),
            executionTimeMs = elapsedMs
        };
    }

    /// <summary>
    /// Normalizes a <see cref="JsonElement"/> input into CLR primitives for the fact bag.
    /// Consumer overrides can use this to parse input JSON before building typed contexts.
    /// </summary>
    protected static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out long l) => l,
            JsonValueKind.Number when element.TryGetDouble(out double d) => d,
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => NormalizeJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }
}
