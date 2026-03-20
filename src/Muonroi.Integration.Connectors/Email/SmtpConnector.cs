using System.Text.Json;
using Muonroi.Integration.Abstractions;
using MailKit.Net.Smtp;
using MimeKit;

namespace Muonroi.Integration.Connectors.Email;

/// <summary>
/// SMTP email connector. Sends emails via SMTP using MailKit.
/// </summary>
public sealed class SmtpConnector : IServiceTaskConnector
{
    /// <summary>
    /// Connector metadata describing capabilities and configuration.
    /// </summary>
    public ConnectorMetadata Metadata => new()
    {
        Type = "email",
        DisplayName = "Email (SMTP)",
        Category = "Communication",
        IconSvg = "<path d=\"M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z\"/>",
        Description = "Send emails via SMTP with configurable server, port, and TLS settings.",
        RequiresCredentials = true
    };

    /// <summary>
    /// Sends an email using the configured SMTP settings.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connector result.</returns>
    public async Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        JsonElement root = context.Config.RootElement;

        string to = root.GetProperty("to").GetString() ?? throw new InvalidOperationException("to is required");
        string subject = root.TryGetProperty("subject", out var s) ? s.GetString() ?? "" : "";
        string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        string from = root.TryGetProperty("from", out var f) ? f.GetString() ?? "noreply@muonroi.dev" : "noreply@muonroi.dev";
        bool isHtml = root.TryGetProperty("isHtml", out var h) && h.GetBoolean();

        string host = context.Credentials.GetValueOrDefault("smtpHost") ?? "localhost";
        int port = int.TryParse(context.Credentials.GetValueOrDefault("smtpPort"), out int p) ? p : 587;
        string? username = context.Credentials.GetValueOrDefault("smtpUsername");
        string? password = context.Credentials.GetValueOrDefault("smtpPassword");

        MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = isHtml
            ? new TextPart("html") { Text = body }
            : new TextPart("plain") { Text = body };

        try
        {
            using SmtpClient client = new();
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable, ct);
            if (!string.IsNullOrEmpty(username))
            {
                await client.AuthenticateAsync(username, password, ct);
            }
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            sw.Stop();
            return ConnectorResult.Ok(new() { ["emailSent"] = true, ["emailTo"] = to }, duration: sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ConnectorResult.Fail($"SMTP error: {ex.Message}", duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Tests connectivity to the SMTP server.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the connection succeeds.</returns>
    public async Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct)
    {
        try
        {
            string host = context.Credentials.GetValueOrDefault("smtpHost") ?? "localhost";
            int port = int.TryParse(context.Credentials.GetValueOrDefault("smtpPort"), out int p) ? p : 587;
            using SmtpClient client = new();
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable, ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch
        {
            return false;
        }
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
                "to": { "type": "string", "format": "email", "description": "Recipient email address" },
                "from": { "type": "string", "format": "email", "default": "noreply@muonroi.dev" },
                "subject": { "type": "string", "description": "Email subject line" },
                "body": { "type": "string", "description": "Email body (supports Scriban template syntax)" },
                "isHtml": { "type": "boolean", "default": false }
            },
            "required": ["to"]
        }
        """;
        return JsonDocument.Parse(schema).RootElement.Clone();
    }
}
