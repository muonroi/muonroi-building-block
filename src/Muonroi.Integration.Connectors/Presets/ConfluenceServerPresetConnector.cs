using System.Text.Json;
using Microsoft.Extensions.Logging;
using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Http;

namespace Muonroi.Integration.Connectors.Presets;

/// <summary>
/// Confluence Server / Data Center preset connector. Delegates 100% to <see cref="HttpConnector"/>.
/// Declares Format="xhtml" and AuthBuilder="bearer" (PAT-based authentication).
/// Uses Confluence Server REST API /rest/api/content (same path as Cloud but Bearer PAT auth).
/// </summary>
public sealed class ConfluenceServerPresetConnector(HttpConnector inner, ILogger<ConfluenceServerPresetConnector>? logger = null) : IServiceTaskConnector
{
    public ConnectorMetadata Metadata => new()
    {
        Type = "confluence-server",
        DisplayName = "Confluence Server / Data Center",
        Category = "Wiki",
        IconSvg = "<path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/>",
        Description = "Confluence Server and Data Center. Uses Personal Access Token (PAT) authentication. Returns page body as XHTML storage format.",
        RequiresCredentials = true,
        Format = "xhtml",
        AuthBuilder = "bearer",
        FieldSchema =
        [
            new ConnectorFieldDescriptor
            {
                Key = "url",
                Label = "Confluence API URL",
                FieldType = "url",
                Placeholder = "https://confluence.yourcompany.com/rest/api/content?expand=body.storage",
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
                Key = "siteUrl",
                Label = "Địa chỉ site",
                FieldType = "url",
                Placeholder = "https://confluence.yourcompany.com",
                Required = true
            },
            new ConnectorFieldDescriptor
            {
                Key = "pat",
                Label = "Mã truy cập (PAT)",
                FieldType = "password",
                Required = true,
                HelpUrl = "https://confluence.atlassian.com/enterprise/using-personal-access-tokens-1026032365.html"
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
        // Derive Confluence Server root from config url — strip /rest/api/content suffix.
        string? configUrl = context.Config.RootElement
            .TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(configUrl)) return null;

        // Strip /rest/api/content (and anything after) to get the base URL.
        string confluenceRoot = configUrl.TrimEnd('/');
        int restIdx = confluenceRoot.IndexOf("/rest/api/content", StringComparison.OrdinalIgnoreCase);
        if (restIdx > 0) confluenceRoot = confluenceRoot[..restIdx];

        // Build content search URL server-side — query built here, BA never sees it.
        System.Text.StringBuilder sb = new($"{confluenceRoot}/rest/api/content?type=page&expand=space");

        if (query.Scope is { Length: > 0 } spaceKey)
            sb.Append($"&spaceKey={Uri.EscapeDataString(spaceKey)}");

        if (query.SearchText is { Length: > 0 } title)
            sb.Append($"&title={Uri.EscapeDataString(title)}");

        if (query.Cursor is { Length: > 0 } start && int.TryParse(start, out int startAt))
            sb.Append($"&start={startAt}");

        sb.Append($"&limit={query.PageSize}");

        string searchUrl = sb.ToString();

        (JsonDocument? doc, bool isPermissionDenied) = await inner.ReadJsonAsync(context, searchUrl, "GET", null, ct);

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

                    string? pageTitle = page.TryGetProperty("title", out JsonElement titleEl) ? titleEl.GetString() : null;

                    string? spaceKeyBreadcrumb = null;
                    if (page.TryGetProperty("space", out JsonElement space) &&
                        space.TryGetProperty("key", out JsonElement sk))
                        spaceKeyBreadcrumb = sk.GetString();

                    string itemUrl = $"{confluenceRoot}/pages/viewpage.action?pageId={id}";

                    items.Add(new ConnectorBrowseItem(
                        ExternalId: id,
                        Title: pageTitle ?? id,
                        Type: "page",
                        LastModified: null,
                        Author: null,
                        Url: itemUrl,
                        Breadcrumb: spaceKeyBreadcrumb));
                }
            }

            // Next cursor via _links.next
            string? nextCursor = null;
            if (root.TryGetProperty("_links", out JsonElement links) &&
                links.TryGetProperty("next", out JsonElement next) &&
                next.ValueKind == JsonValueKind.String)
            {
                string? nextHref = next.GetString();
                if (!string.IsNullOrWhiteSpace(nextHref))
                    nextCursor = nextHref; // opaque cursor — caller passes it as Cursor on next page
            }

            doc.Dispose();
            return new ConnectorBrowseResult(items, nextCursor, IsPermissionDenied: false);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "[ConfluenceServerPresetConnector] ListDocumentsAsync JSON mapping failed. module=ConfluenceServerPresetConnector op=ListDocumentsAsync type=confluence-server");
            doc?.Dispose();
            return new ConnectorBrowseResult([], null, IsPermissionDenied: false);
        }
    }
}
