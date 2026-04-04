using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation;

/// <summary>
/// Post-execution webhook notification service.
/// The concrete implementation lives in the control-plane (muonroi-control-plane).
/// Building-block only defines the interface — webhook is optional/skipped when not registered.
/// </summary>
public interface IWebhookNotificationService
{
    /// <summary>
    /// Sends a webhook POST notification to the given URL with the scenario execution payload.
    /// Implementations should be resilient — caller wraps this in fire-and-forget.
    /// </summary>
    Task NotifyAsync(string webhookUrl, WebhookPayload payload, CancellationToken ct = default);
}

/// <summary>
/// Payload sent to external webhook endpoints after scenario execution completes.
/// </summary>
public sealed record WebhookPayload
{
    /// <summary>Identifier of the executed scenario.</summary>
    public required string ScenarioId { get; init; }

    /// <summary>Name of the workflow/ruleset that was tested.</summary>
    public required string WorkflowName { get; init; }

    /// <summary>Execution outcome: "passed", "failed", or "error".</summary>
    public required string Status { get; init; }

    /// <summary>Input facts provided to the scenario.</summary>
    public JsonElement? InputFacts { get; init; }

    /// <summary>Output facts produced by the execution.</summary>
    public JsonElement? OutputFacts { get; init; }

    /// <summary>Wall-clock duration of the scenario execution.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether the result matched the scenario's expected behavior.</summary>
    public bool MatchesExpectation { get; init; }

    /// <summary>UTC timestamp when the scenario was executed.</summary>
    public DateTimeOffset ExecutedAt { get; init; }
}
