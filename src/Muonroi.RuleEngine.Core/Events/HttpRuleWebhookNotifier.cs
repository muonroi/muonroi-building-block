using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Muonroi.Logging.Abstractions;

namespace Muonroi.RuleEngine.Core.Events;

/// <summary>
/// Sends CloudEvents to a webhook URL via HTTP POST with exponential backoff retry
/// on transient failures (5xx responses and network errors).
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="HttpRuleWebhookNotifier"/>.
/// </remarks>
/// <param name="httpClientFactory">Factory for creating named HTTP clients.</param>
/// <param name="options">Webhook configuration options.</param>
/// <param name="logger">Optional structured logger.</param>
public sealed class HttpRuleWebhookNotifier(
    IHttpClientFactory httpClientFactory,
    IOptions<WebhookOptions> options,
    IMLog<HttpRuleWebhookNotifier>? logger = null) : IRuleWebhookNotifier
{
    private const string CloudEventsContentType = "application/cloudevents+json";
    private const string HttpClientName = "RuleWebhook";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly WebhookOptions _options = options.Value;
    private readonly IMLog<HttpRuleWebhookNotifier>? _logger = logger;

    /// <inheritdoc />
    public async Task<WebhookResult> NotifyAsync(CloudEvent cloudEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
            return new WebhookResult(false, 0, "Webhook URL is not configured.");

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var json = JsonSerializer.Serialize(cloudEvent);
        var maxAttempts = _options.MaxRetries + 1;
        string? lastError = null;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                // Exponential backoff: delay = initialDelay * 2^(attempt-1)
                var delay = _options.InitialRetryDelay * Math.Pow(2, attempt - 1);
                _logger?.Warn("Webhook attempt {Attempt}/{Max} failed. Retrying in {Delay}ms. Error: {Error}",
                    attempt, maxAttempts, (int)delay.TotalMilliseconds, lastError);

                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    return new WebhookResult(false, attempt, "Operation was cancelled during retry wait.");
                }
            }

            try
            {
                using var request = BuildRequest(json);
                using var response = await client.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    _logger?.Info("Webhook delivered successfully. Type={Type} Attempts={Attempts}",
                        cloudEvent.Type, attempt + 1);
                    return new WebhookResult(true, attempt + 1);
                }

                // 4xx — client error, do NOT retry
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    lastError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} — client error, not retrying.";
                    _logger?.Error(null, "Webhook permanent failure. Type={Type} Status={Status}",
                        cloudEvent.Type, (int)response.StatusCode);
                    return new WebhookResult(false, attempt + 1, lastError);
                }

                // 5xx — server error, will retry
                lastError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return new WebhookResult(false, attempt + 1, "Operation was cancelled.");
            }
            catch (HttpRequestException ex)
            {
                lastError = ex.Message;
            }
        }

        _logger?.Error(null, "Webhook delivery failed after {Attempts} attempts. Type={Type} LastError={Error}",
            maxAttempts, cloudEvent.Type, lastError);
        return new WebhookResult(false, maxAttempts, lastError);
    }

    private HttpRequestMessage BuildRequest(string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _options.Url)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, CloudEventsContentType)
        };

        foreach (var (key, value) in _options.Headers)
            request.Headers.TryAddWithoutValidation(key, value);

        return request;
    }
}
