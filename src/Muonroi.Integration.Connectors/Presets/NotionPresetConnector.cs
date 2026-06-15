using System.Text.Json;
using Microsoft.Extensions.Logging;
using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Http;

namespace Muonroi.Integration.Connectors.Presets;

/// <summary>
/// Notion preset connector. Delegates 100% to <see cref="HttpConnector"/>.
/// Declares AuthBuilder="bearer" (Integration Token authentication).
/// </summary>
public sealed class NotionPresetConnector(HttpConnector inner, ILogger<NotionPresetConnector>? logger = null) : IServiceTaskConnector
{
    public ConnectorMetadata Metadata => new()
    {
        Type = "notion",
        DisplayName = "Notion",
        Category = "Wiki",
        IconSvg = "<path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/>",
        Description = "Notion pages and databases via the Notion API. Uses Integration Token authentication.",
        RequiresCredentials = true,
        Format = "plain",
        AuthBuilder = "bearer",
        FieldSchema =
        [
            new ConnectorFieldDescriptor
            {
                Key = "url",
                Label = "Notion API URL",
                FieldType = "url",
                Placeholder = "https://api.notion.com/v1/pages/{page_id}",
                Required = true
            },
            new ConnectorFieldDescriptor
            {
                Key = "method",
                Label = "HTTP Method",
                FieldType = "text",
                Placeholder = "GET",
                Required = false
            },
        ],
        CredentialFields =
        [
            new ConnectorFieldDescriptor
            {
                Key = "integrationToken",
                Label = "Integration Token",
                FieldType = "password",
                Required = true,
                HelpUrl = "https://www.notion.so/my-integrations"
            },
        ]
    };

    public Task<ConnectorResult> ExecuteAsync(ConnectorContext ctx, CancellationToken ct)
        => inner.ExecuteAsync(ctx, ct);

    public Task<bool> TestConnectionAsync(ConnectorContext ctx, CancellationToken ct)
        => inner.TestConnectionAsync(ctx, ct);

    public System.Text.Json.JsonElement GetConfigSchema()
        => inner.GetConfigSchema();

    /// <inheritdoc/>
    public async Task<ConnectorBrowseResult?> ListDocumentsAsync(
        ConnectorContext context,
        ConnectorBrowseQuery query,
        CancellationToken ct)
    {
        // Notion base URL: derive from config, strip page/database path suffix.
        string? configUrl = context.Config.RootElement
            .TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(configUrl)) return null;

        // Notion API is always at api.notion.com — derive root regardless of per-page path stored in config.
        Uri configUri = new(configUrl);
        string notionRoot = $"{configUri.Scheme}://{configUri.Host}";
        string searchUrl = $"{notionRoot}/v1/search";

        // Build search body server-side — BA never sees it (BROWSE-01).
        System.Text.StringBuilder bodyBuilder = new("{");
        if (query.SearchText is { Length: > 0 } q)
            bodyBuilder.Append($"\"query\":\"{q.Replace("\"", "\\\"")}\",");
        bodyBuilder.Append("\"filter\":{\"property\":\"object\",\"value\":\"page\"},");
        bodyBuilder.Append($"\"page_size\":{query.PageSize}");
        if (query.Cursor is { Length: > 0 } cursor)
            bodyBuilder.Append($",\"start_cursor\":\"{cursor}\"");
        bodyBuilder.Append('}');

        string jsonBody = bodyBuilder.ToString();

        (JsonDocument? doc, bool isPermissionDenied) = await inner.ReadJsonAsync(context, searchUrl, "POST", jsonBody, ct);

        if (isPermissionDenied)
            return new ConnectorBrowseResult([], null, IsPermissionDenied: true);

        if (doc is null)
            return new ConnectorBrowseResult([], null, IsPermissionDenied: false);

        try
        {
            JsonElement root = doc.RootElement;
            List<ConnectorBrowseItem> items = [];

            if (root.TryGetProperty("results", out JsonElement results))
            {
                foreach (JsonElement page in results.EnumerateArray())
                {
                    string? id = page.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                    if (id is null) continue;

                    // Extract title from properties.title (rich_text array) or Name property.
                    string? title = ExtractNotionTitle(page);

                    items.Add(new ConnectorBrowseItem(
                        ExternalId: id,
                        Title: title ?? id,
                        Type: "page",
                        LastModified: null,
                        Author: null,
                        Url: page.TryGetProperty("url", out JsonElement uEl) ? uEl.GetString() : null,
                        Breadcrumb: null));
                }
            }

            string? nextCursor = root.TryGetProperty("next_cursor", out JsonElement nc) &&
                nc.ValueKind == JsonValueKind.String ? nc.GetString() : null;

            doc.Dispose();
            return new ConnectorBrowseResult(items, nextCursor, IsPermissionDenied: false);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "[NotionPresetConnector] ListDocumentsAsync JSON mapping failed. module=NotionPresetConnector op=ListDocumentsAsync type=notion");
            doc?.Dispose();
            return new ConnectorBrowseResult([], null, IsPermissionDenied: false);
        }
    }

    private static string? ExtractNotionTitle(JsonElement page)
    {
        if (!page.TryGetProperty("properties", out JsonElement props))
            return null;

        // Try standard "title" property first, then "Name"
        foreach (string key in new[] { "title", "Name" })
        {
            if (!props.TryGetProperty(key, out JsonElement prop)) continue;

            // Title property is an object with a "title" array of rich_text
            if (prop.TryGetProperty("title", out JsonElement richTextArr) &&
                richTextArr.ValueKind == JsonValueKind.Array)
            {
                System.Text.StringBuilder sb = new();
                foreach (JsonElement rt in richTextArr.EnumerateArray())
                {
                    if (rt.TryGetProperty("plain_text", out JsonElement pt))
                        sb.Append(pt.GetString());
                }
                string result = sb.ToString().Trim();
                if (result.Length > 0) return result;
            }
        }

        return null;
    }
}
