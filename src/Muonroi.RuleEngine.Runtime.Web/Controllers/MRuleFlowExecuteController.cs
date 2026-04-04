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

        // Build reverse map: ruleCode → nodeId from __graph.node.{nodeId}.executed keys.
        // Graph-based flow nodes use nodeId (e.g., "rule-liner") while RuleResults uses
        // rule code (e.g., "FCD_V4_LINER_VALID"). For flow-only nodes (actions, conditions
        // without code-first rules), nodeId == ruleCode so the map entry is identity.
        const string graphNodePrefix = "__graph.node.";
        const string executedSuffix = ".executed";
        HashSet<string> graphNodeIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (string key in allFacts.Keys)
        {
            if (key.StartsWith(graphNodePrefix, StringComparison.OrdinalIgnoreCase) &&
                key.EndsWith(executedSuffix, StringComparison.OrdinalIgnoreCase))
            {
                string nodeId = key[graphNodePrefix.Length..^executedSuffix.Length];
                graphNodeIds.Add(nodeId);
            }
        }

        // Map ruleCode → matching graphNodeId by checking which nodeId has .executed = true
        // and is NOT already a direct match. For code-first rules wrapped in graph nodes,
        // the graph node's result message or the RuleResults ordering helps correlate.
        // Simplest reliable approach: for each ruleCode not in graphNodeIds, check if there's
        // a graphNodeId that is not in RuleResults keys — then pair them by execution order.
        HashSet<string> ruleResultKeys = new(result.RuleResults.Keys, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> ruleCodeToNodeId = new(StringComparer.OrdinalIgnoreCase);

        // Direct matches: ruleCode IS a nodeId (flow-only nodes like act-count, cond-overweight)
        foreach (string ruleCode in ruleResultKeys)
        {
            if (graphNodeIds.Contains(ruleCode))
                ruleCodeToNodeId[ruleCode] = ruleCode;
        }

        // For remaining: match unmapped ruleCode ↔ unmapped nodeId by FactBag result correlation.
        // A graph node stores its result at __graph.node.{nodeId}.result — if the result contains
        // the rule's success/failure state, they match. Use execution order as fallback.
        HashSet<string> mappedNodeIds = new(ruleCodeToNodeId.Values, StringComparer.OrdinalIgnoreCase);
        List<string> unmappedRuleCodes = ruleResultKeys.Where(rc => !ruleCodeToNodeId.ContainsKey(rc)).ToList();
        List<string> unmappedNodeIds = graphNodeIds.Where(nid => !mappedNodeIds.Contains(nid)).ToList();

        // Correlate by checking __graph.node.{nodeId}.passed matches RuleResult.IsSuccess
        // When multiple matches exist, use ordering (both are execution-order).
        if (unmappedRuleCodes.Count > 0 && unmappedNodeIds.Count > 0)
        {
            // Both lists maintain execution order — zip them sequentially
            for (int i = 0; i < Math.Min(unmappedRuleCodes.Count, unmappedNodeIds.Count); i++)
            {
                ruleCodeToNodeId[unmappedRuleCodes[i]] = unmappedNodeIds[i];
            }
        }

        return new
        {
            isSuccess = result.IsSuccess,
            errors = result.Errors,
            results = result.RuleResults.Select(kvp =>
            {
                string ruleCode = kvp.Key;
                // Use mapped nodeId for graph key lookup, fall back to ruleCode itself
                string nodeId = ruleCodeToNodeId.TryGetValue(ruleCode, out string? mapped) ? mapped : ruleCode;

                Dictionary<string, object?> graphFacts = new(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, object?> businessFacts = new(StringComparer.OrdinalIgnoreCase);
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

                // Build structured status from __graph.node.{nodeId}.* execution metadata
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
                    message = statusMessage,
                    elapsedMs = (object?)null  // not available from graph execution
                };

                // Read per-node trace snapshots captured by GraphRuleDispatchAdapter
                var traceInput = allFacts.TryGetValue($"__trace.node.{nodeId}.input", out object? tiVal)
                    ? tiVal as IDictionary<string, object?> : null;
                var traceOutput = allFacts.TryGetValue($"__trace.node.{nodeId}.output", out object? toVal)
                    ? toVal as IDictionary<string, object?> : null;

                return new
                {
                    ruleName = ruleCode,
                    isSuccess = kvp.Value.IsSuccess,
                    evaluationResult = kvp.Value.IsSuccess,
                    errors = kvp.Value.Errors,
                    outputs = nodeOutputs.Count > 0 ? (object)nodeOutputs : null,
                    status = status,
                    businessFacts = businessFacts.Count > 0 ? (object)businessFacts : null,
                    inputSnapshot = traceInput?.Count > 0 ? (object)traceInput : null,
                    outputSnapshot = traceOutput?.Count > 0 ? (object)traceOutput : null
                };
            }),
            factBag = allFacts
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase),
            factBagClean = allFacts
                .Where(kvp => !kvp.Key.StartsWith("__graph.", StringComparison.OrdinalIgnoreCase)
                           && !kvp.Key.StartsWith("__trace.", StringComparison.OrdinalIgnoreCase))
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
