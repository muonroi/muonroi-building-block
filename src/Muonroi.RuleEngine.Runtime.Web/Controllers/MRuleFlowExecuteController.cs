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
                errorMessage = t.FailReason,
                // Structured per-node trace data (additive — old FE ignores unknown fields)
                inputSnapshot = t.InputFactsJson != null
                    ? (object?)System.Text.Json.JsonSerializer.Deserialize<object>(t.InputFactsJson)
                    : null,
                outputSnapshot = t.OutputFactsJson != null
                    ? (object?)System.Text.Json.JsonSerializer.Deserialize<object>(t.OutputFactsJson)
                    : null,
                changedKeys = t.ChangedFactKeys,
                elapsedMs = t.ElapsedMs
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
        IReadOnlyDictionary<string, object?> allFacts = result.Facts.AsReadOnly();

        return new
        {
            isSuccess = result.IsSuccess,
            errors = result.Errors,
            results = result.RuleResults.Select(kvp =>
            {
                // Separate __graph.node.{id}.* (execution metadata) from __node.{id}.* (business facts)
                string nodeId = kvp.Key;
                Dictionary<string, object?> graphFacts = new(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, object?> businessFacts = new(StringComparer.OrdinalIgnoreCase);
                // Keep combined outputs for backward compatibility
                Dictionary<string, object?> nodeOutputs = new(StringComparer.OrdinalIgnoreCase);
                string graphPrefix = $"__graph.node.{nodeId}.";
                string nodePrefix = $"__node.{nodeId}.";

                foreach ((string key, object? value) in allFacts)
                {
                    if (key.StartsWith(graphPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string shortKey = key[graphPrefix.Length..];
                        graphFacts[shortKey] = value;
                        nodeOutputs[shortKey] = value;
                    }
                    else if (key.StartsWith(nodePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string shortKey = key[nodePrefix.Length..];
                        businessFacts[shortKey] = value;
                        nodeOutputs[shortKey] = value;
                    }
                }

                // Build structured status from __graph.node.{id}.* execution metadata
                _ = graphFacts.TryGetValue("result", out object? resultObj);
                string? statusMessage = (resultObj as IDictionary<string, object?>)
                    ?.TryGetValue("message", out object? msgObj) == true
                    ? msgObj?.ToString()
                    : null;
                var status = new
                {
                    executed = graphFacts.TryGetValue("executed", out object? execVal) && execVal is true,
                    passed = graphFacts.TryGetValue("passed", out object? passedVal) && passedVal is true,
                    errored = graphFacts.TryGetValue("errored", out object? erroredVal) && erroredVal is true,
                    message = statusMessage
                };

                return new
                {
                    ruleName = kvp.Key,
                    isSuccess = kvp.Value.IsSuccess,
                    evaluationResult = kvp.Value.IsSuccess,
                    errors = kvp.Value.Errors,
                    outputs = nodeOutputs.Count > 0 ? (object)nodeOutputs : null,  // backward compat
                    status = status,
                    businessFacts = businessFacts.Count > 0 ? (object)businessFacts : null
                };
            }),
            factBag = allFacts
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase),
            // Clean factBag without internal __graph.* metadata keys
            factBagClean = allFacts
                .Where(kvp => !kvp.Key.StartsWith("__graph.", StringComparison.OrdinalIgnoreCase))
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
