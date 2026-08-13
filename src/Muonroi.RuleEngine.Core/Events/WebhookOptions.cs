namespace Muonroi.RuleEngine.Core.Events;

/// <summary>
/// Configuration options for the rule engine webhook notifier.
/// </summary>
public sealed class WebhookOptions
{
    /// <summary>
    /// The target webhook URL to POST CloudEvents to.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of retry attempts on transient failures (5xx / network errors).
    /// Defaults to 3. A value of 0 means no retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// The initial delay before the first retry attempt.
    /// Subsequent retries use exponential backoff: InitialRetryDelay * 2^attempt.
    /// Defaults to 1 second.
    /// </summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Per-request HTTP timeout.
    /// Defaults to 10 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Additional HTTP headers to include in every outbound webhook request.
    /// Useful for shared secrets, authorization tokens, or correlation IDs.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];
}
