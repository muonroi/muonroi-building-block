using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Core.Events;
using Muonroi.RuleEngine.Runtime.Rules;

namespace Muonroi.RuleEngine.Runtime.Events;

/// <summary>
/// Decorator over <see cref="IRuleSetChangeNotifier"/> that additionally publishes a
/// CloudEvents 1.0 envelope to an <see cref="IEventSink"/> on every ruleset change.
/// </summary>
/// <remarks>
/// Initializes a new <see cref="CloudEventPublishingNotifier"/>.
/// </remarks>
/// <param name="inner">The inner notifier to delegate to first.</param>
/// <param name="eventSink">The event sink to publish CloudEvents to.</param>
/// <param name="log">Optional structured logger.</param>
public sealed class CloudEventPublishingNotifier(
    IRuleSetChangeNotifier inner,
    IEventSink eventSink,
    IMLog<CloudEventPublishingNotifier>? log = null) : IRuleSetChangeNotifier
{
    private const string EventSource = "/muonroi/rule-engine";

    private readonly IRuleSetChangeNotifier _inner = MGuard.NotNull(inner);
    private readonly IEventSink _eventSink = MGuard.NotNull(eventSink);
    private readonly IMLog<CloudEventPublishingNotifier>? _log = log;

    /// <inheritdoc />
    public async Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default)
    {
        // Delegate to inner first (hot-reload, SignalR, Redis, etc.)
        await _inner.PublishAsync(changeEvent, cancellationToken);

        // Then publish the CloudEvent to the configured sink
        CloudEvent cloudEvent = BuildCloudEvent(changeEvent);
        try
        {
            await _eventSink.PublishAsync(cloudEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _log?.Warn("Failed to publish CloudEvent for ruleset change {ChangeType}/{WorkflowName}: {Error}",
                changeEvent.ChangeType, changeEvent.WorkflowName, ex.Message);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler)
        => _inner.Subscribe(handler);

    private static CloudEvent BuildCloudEvent(RuleSetChangeEvent changeEvent)
    {
        string cloudEventType = MapChangeTypeToCloudEventType(changeEvent.ChangeType);

        return new CloudEvent
        {
            Type = cloudEventType,
            Source = EventSource,
            Subject = $"{changeEvent.TenantId}/{changeEvent.WorkflowName}",
            Time = changeEvent.OccurredAtUtc,
            Data = changeEvent
        };
    }

    private static string MapChangeTypeToCloudEventType(string changeType)
    {
        return changeType switch
        {
            RuleSetChangeTypes.Activated => RuleCloudEventTypes.RuleSetActivated,
            _ => RuleCloudEventTypes.RuleSetChanged
        };
    }
}
