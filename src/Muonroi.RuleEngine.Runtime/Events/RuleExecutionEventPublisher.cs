using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Core.Events;

namespace Muonroi.RuleEngine.Runtime.Events;

/// <summary>
/// Publishes CloudEvents for rule execution results (executed/failed).
/// Fire-and-forget safe: exceptions from the sink are caught and logged, never propagated.
/// </summary>
/// <remarks>
/// Initializes a new <see cref="RuleExecutionEventPublisher"/>.
/// </remarks>
/// <param name="eventSink">The event sink to publish to.</param>
/// <param name="log">Optional structured logger.</param>
public sealed class RuleExecutionEventPublisher(
    IEventSink eventSink,
    IMLog<RuleExecutionEventPublisher>? log = null)
{
    private const string EventSource = "/muonroi/rule-engine";

    private readonly IEventSink _eventSink = MGuard.NotNull(eventSink);
    private readonly IMLog<RuleExecutionEventPublisher>? _log = log;

    /// <summary>
    /// Publishes a rule execution result as a CloudEvent.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="workflowName">Workflow name that was evaluated.</param>
    /// <param name="success">Whether the execution succeeded.</param>
    /// <param name="ruleCount">Number of rules that were evaluated.</param>
    /// <param name="elapsedMs">Wall-clock duration in milliseconds.</param>
    /// <param name="errorMessage">Error message when <paramref name="success"/> is false.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PublishExecutionResultAsync(
        string tenantId,
        string workflowName,
        bool success,
        int ruleCount,
        double elapsedMs,
        string? errorMessage,
        CancellationToken ct = default)
    {
        try
        {
            string cloudEventType = success
                ? RuleCloudEventTypes.RuleExecuted
                : RuleCloudEventTypes.RuleFailed;

            CloudEvent cloudEvent = new()
            {
                Type = cloudEventType,
                Source = EventSource,
                Subject = $"{tenantId}/{workflowName}",
                Data = new
                {
                    tenantId,
                    workflowName,
                    ruleCount,
                    elapsedMs,
                    errorMessage
                }
            };

            await _eventSink.PublishAsync(cloudEvent, ct);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: never propagate exceptions from the publisher.
            _log?.Warn("Failed to publish execution result CloudEvent for {WorkflowName}: {Error}",
                workflowName, ex.Message);
        }
    }
}
