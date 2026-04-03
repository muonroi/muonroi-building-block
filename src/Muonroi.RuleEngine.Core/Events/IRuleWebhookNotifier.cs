namespace Muonroi.RuleEngine.Core.Events;

/// <summary>
/// Defines a notifier that delivers a <see cref="CloudEvent"/> to a webhook endpoint via HTTP.
/// </summary>
public interface IRuleWebhookNotifier
{
    /// <summary>
    /// Sends a CloudEvent to the configured webhook URL with retry/backoff on transient failures.
    /// </summary>
    /// <param name="cloudEvent">The event envelope to deliver.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="WebhookResult"/> describing success or failure.</returns>
    Task<WebhookResult> NotifyAsync(CloudEvent cloudEvent, CancellationToken ct = default);
}

/// <summary>
/// Represents the outcome of a webhook delivery attempt.
/// </summary>
/// <param name="Success">Whether the delivery was ultimately successful.</param>
/// <param name="Attempts">Total number of HTTP attempts made (1 = no retry needed).</param>
/// <param name="ErrorMessage">Optional error message on failure.</param>
public sealed record WebhookResult(bool Success, int Attempts, string? ErrorMessage = null);
