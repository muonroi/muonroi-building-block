using System.Text;
using System.Text.Json;
using Muonroi.Integration.Abstractions;

namespace Muonroi.Integration.Connectors.Slack;

/// <summary>
/// Slack incoming webhook connector.
/// </summary>
/// <remarks>
/// Creates a Slack webhook connector.
/// </remarks>
/// <param name="httpClientFactory">Factory used to create HTTP clients.</param>
public sealed class SlackWebhookConnector(IHttpClientFactory httpClientFactory) : IServiceTaskConnector
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    /// <summary>
    /// Connector metadata describing capabilities and configuration.
    /// </summary>
    public ConnectorMetadata Metadata => new()
    {
        Type = "slack",
        DisplayName = "Slack Webhook",
        Category = "Communication",
        IconSvg = "<path d=\"M5.042 15.165a2.528 2.528 0 0 1-2.52 2.523A2.528 2.528 0 0 1 0 15.165a2.527 2.527 0 0 1 2.522-2.52h2.52v2.52zM6.313 15.165a2.527 2.527 0 0 1 2.521-2.52 2.527 2.527 0 0 1 2.521 2.52v6.313A2.528 2.528 0 0 1 8.834 24a2.528 2.528 0 0 1-2.521-2.522v-6.313z\"/>",
        Description = "Send messages to Slack channels via incoming webhooks.",
        RequiresCredentials = true
    };

    /// <summary>
    /// Sends a message to Slack via webhook.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connector result.</returns>
    public async Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        JsonElement root = context.Config.RootElement;

        string? credWebhook = context.Credentials.GetValueOrDefault("webhookUrl");
        string webhookUrl = !string.IsNullOrEmpty(credWebhook)
            ? credWebhook
            : (root.TryGetProperty("webhookUrl", out var w) ? w.GetString() ?? "" : "");
        string text = root.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
        string? channel = root.TryGetProperty("channel", out var c) ? c.GetString() : null;

        if (string.IsNullOrEmpty(webhookUrl))
        {
            return ConnectorResult.Fail("Slack webhook URL is required.");
        }

        var payload = new Dictionary<string, string> { ["text"] = text };
        if (!string.IsNullOrEmpty(channel)) payload["channel"] = channel;

        string json = JsonSerializer.Serialize(payload);
        HttpClient client = _httpClientFactory.CreateClient("MuonroiConnector");

        try
        {
            HttpResponseMessage response = await client.PostAsync(
                webhookUrl,
                new StringContent(json, Encoding.UTF8, "application/json"),
                ct);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return ConnectorResult.Ok(new() { ["slackSent"] = true }, (int)response.StatusCode, sw.Elapsed);
            }

            string body = await response.Content.ReadAsStringAsync(ct);
            return ConnectorResult.Fail($"Slack API error: {body}", (int)response.StatusCode, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ConnectorResult.Fail($"Slack error: {ex.Message}", duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Validates the webhook URL format.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the webhook URL looks valid.</returns>
    public async Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct)
    {
        // Slack doesn't have a test endpoint; we just validate the URL format
        string webhookUrl = context.Credentials.GetValueOrDefault("webhookUrl") ?? "";
        return await Task.FromResult(
            Uri.TryCreate(webhookUrl, UriKind.Absolute, out Uri? uri) &&
            uri.Host.Contains("slack", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the JSON schema used to configure this connector.
    /// </summary>
    /// <returns>Configuration schema.</returns>
    public JsonElement GetConfigSchema()
    {
        string schema = """
        {
            "type": "object",
            "properties": {
                "text": { "type": "string", "description": "Message text (supports Scriban template syntax)" },
                "channel": { "type": "string", "description": "Override channel (optional)" }
            },
            "required": ["text"]
        }
        """;
        return JsonDocument.Parse(schema).RootElement.Clone();
    }
}
