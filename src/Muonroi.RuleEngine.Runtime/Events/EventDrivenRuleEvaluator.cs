using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core.Events;
using Muonroi.RuleEngine.Runtime.Rules;

namespace Muonroi.RuleEngine.Runtime.Events;

/// <summary>
/// Evaluates rules triggered by incoming CloudEvents.
/// Extracts the target workflow name and input facts from the event data,
/// then delegates to <see cref="RulesEngineService"/> for rule execution.
/// </summary>
public sealed class EventDrivenRuleEvaluator : IEventDrivenRuleEvaluator
{
    private readonly RulesEngineService _rulesEngine;
    private readonly RuleExecutionEventPublisher? _eventPublisher;
    private readonly IMLog<EventDrivenRuleEvaluator>? _log;

    /// <summary>
    /// Initializes a new <see cref="EventDrivenRuleEvaluator"/>.
    /// </summary>
    /// <param name="rulesEngine">The rule engine service to evaluate rules with.</param>
    /// <param name="eventPublisher">Optional publisher for execution result events.</param>
    /// <param name="log">Optional structured logger.</param>
    public EventDrivenRuleEvaluator(
        RulesEngineService rulesEngine,
        RuleExecutionEventPublisher? eventPublisher = null,
        IMLog<EventDrivenRuleEvaluator>? log = null)
    {
        _rulesEngine = MGuard.NotNull(rulesEngine);
        _eventPublisher = eventPublisher;
        _log = log;
    }

    /// <inheritdoc />
    public async Task<EventEvaluationResult> HandleEventAsync(CloudEvent cloudEvent, CancellationToken ct = default)
    {
        MGuard.NotNull(cloudEvent);

        // Extract workflowName and inputFacts from the event data
        if (!TryExtractEventData(cloudEvent.Data, out string? workflowName, out Dictionary<string, object?> inputFacts))
        {
            _log?.Debug("Skipping CloudEvent {EventId}: missing or unreadable workflowName in Data", cloudEvent.Id);
            return new EventEvaluationResult(EventEvaluationStatus.Skipped);
        }

        long startMs = System.Diagnostics.Stopwatch.GetTimestamp();

        try
        {
            // Evaluate rules using the input facts as both context and initial FactBag seed
            Dictionary<string, object?> initialFacts = inputFacts ?? [];

            OrchestratorResult result = await _rulesEngine.ExecuteWithResultAsync(
                workflowName!,
                initialFacts,
                initialFacts as IReadOnlyDictionary<string, object?>,
                ct);

            double elapsedMs = CalculateElapsedMs(startMs);
            int ruleCount = result.RuleResults.Count;

            if (_eventPublisher is not null)
            {
                result.Facts.TryGet<string>("__tenantId", out string? tenantIdFact);
                await _eventPublisher.PublishExecutionResultAsync(
                    tenantId: tenantIdFact ?? string.Empty,
                    workflowName: workflowName!,
                    success: result.IsSuccess,
                    ruleCount: ruleCount,
                    elapsedMs: elapsedMs,
                    errorMessage: result.IsSuccess ? null : string.Join("; ", result.Errors),
                    ct: ct);
            }

            // Collect output facts from the FactBag keys
            IDictionary<string, object?> outputFacts = result.Facts.Keys
                .ToDictionary(k => k, k => result.Facts[k]);

            if (result.IsSuccess)
            {
                return new EventEvaluationResult(EventEvaluationStatus.Success, outputFacts);
            }

            string errorMessage = string.Join("; ", result.Errors);
            return new EventEvaluationResult(EventEvaluationStatus.Failed, outputFacts, errorMessage);
        }
        catch (Exception ex)
        {
            double elapsedMs = CalculateElapsedMs(startMs);
            _log?.Error(ex, "Exception evaluating rules for workflow {WorkflowName} from CloudEvent {EventId}",
                workflowName, cloudEvent.Id);

            if (_eventPublisher is not null)
            {
                await _eventPublisher.PublishExecutionResultAsync(
                    tenantId: string.Empty,
                    workflowName: workflowName!,
                    success: false,
                    ruleCount: 0,
                    elapsedMs: elapsedMs,
                    errorMessage: ex.Message,
                    ct: ct);
            }

            return new EventEvaluationResult(EventEvaluationStatus.Failed, null, ex.Message);
        }
    }

    /// <summary>
    /// Tries to extract <c>workflowName</c> and <c>inputFacts</c> from the CloudEvent data payload.
    /// Supports data as <see cref="JsonElement"/> (common when deserialized from JSON transport)
    /// or as an anonymous/typed object.
    /// </summary>
    private static bool TryExtractEventData(
        object? data,
        out string? workflowName,
        out Dictionary<string, object?> inputFacts)
    {
        workflowName = null;
        inputFacts = [];

        if (data is null)
        {
            return false;
        }

        if (data is JsonElement jsonElement)
        {
            return TryExtractFromJsonElement(jsonElement, out workflowName, out inputFacts);
        }

        // Try via reflection for anonymous/typed objects
        Type dataType = data.GetType();
        System.Reflection.PropertyInfo? wfProp = dataType.GetProperty("workflowName")
            ?? dataType.GetProperty("WorkflowName");
        workflowName = wfProp?.GetValue(data) as string;

        if (string.IsNullOrWhiteSpace(workflowName))
        {
            return false;
        }

        System.Reflection.PropertyInfo? factsProp = dataType.GetProperty("inputFacts")
            ?? dataType.GetProperty("InputFacts");
        if (factsProp?.GetValue(data) is Dictionary<string, object?> facts)
        {
            inputFacts = facts;
        }

        return true;
    }

    private static bool TryExtractFromJsonElement(
        JsonElement element,
        out string? workflowName,
        out Dictionary<string, object?> inputFacts)
    {
        workflowName = null;
        inputFacts = [];

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Extract workflowName (case-insensitive property lookup)
        if (!TryGetJsonStringProperty(element, "workflowName", out workflowName)
            && !TryGetJsonStringProperty(element, "WorkflowName", out workflowName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(workflowName))
        {
            return false;
        }

        // Extract inputFacts — optional dictionary
        if (element.TryGetProperty("inputFacts", out JsonElement factsEl)
            || element.TryGetProperty("InputFacts", out factsEl))
        {
            if (factsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty prop in factsEl.EnumerateObject())
                {
                    inputFacts[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.TryGetInt64(out long l) ? (object?)l : prop.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => prop.Value.GetRawText()
                    };
                }
            }
        }

        return true;
    }

    private static bool TryGetJsonStringProperty(JsonElement element, string name, out string? value)
    {
        if (element.TryGetProperty(name, out JsonElement prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static double CalculateElapsedMs(long startTicks)
    {
        long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
        return elapsed * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }
}
