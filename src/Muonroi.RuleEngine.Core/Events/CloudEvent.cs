namespace Muonroi.RuleEngine.Core.Events;

/// <summary>
/// Represents a CloudEvents 1.0 compliant envelope for rule engine events.
/// See: https://cloudevents.io/
/// </summary>
public sealed record CloudEvent
{
    /// <summary>
    /// Identifies the event. MUST be unique within the producer context.
    /// Defaults to a new GUID formatted as a 32-character hex string.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The version of the CloudEvents specification.
    /// Always "1.0" for this implementation.
    /// </summary>
    public string SpecVersion { get; init; } = "1.0";

    /// <summary>
    /// The event type. SHOULD be prefixed with a reverse-DNS name.
    /// Example: "muonroi.ruleset.changed"
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Identifies the context in which an event happened.
    /// Example: "/muonroi/rule-engine"
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// The timestamp of when the event occurred, in UTC.
    /// Defaults to DateTimeOffset.UtcNow.
    /// </summary>
    public DateTimeOffset Time { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The media type of the data field.
    /// Defaults to "application/json".
    /// </summary>
    public string DataContentType { get; init; } = "application/json";

    /// <summary>
    /// Event-type-specific subject context for the event.
    /// Optional. Example: "{tenantId}/{workflowName}"
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// The event payload. May be any serializable object.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Optional CloudEvents extension attributes (key-value pairs).
    /// Used for protocol-specific or domain-specific attributes.
    /// </summary>
    public IDictionary<string, string>? Extensions { get; init; }
}
