using System.Text.Json;
using Microsoft.Extensions.Logging;
using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Http;

namespace Muonroi.Integration.Connectors.Presets;

/// <summary>
/// GitHub preset connector. Delegates 100% to <see cref="HttpConnector"/>.
/// Declares Format="markdown" and AuthBuilder="bearer" (PAT authentication).
/// ListDocumentsAsync uses GET /search/code?q={q}+repo:{owner}/{repo} to browse files.
/// </summary>
public sealed class GitHubPresetConnector(HttpConnector inner, ILogger<GitHubPresetConnector>? logger = null) : IServiceTaskConnector
{
    /// <summary>
    /// Connector metadata describing GitHub capabilities and configuration schema.
    /// </summary>
    public ConnectorMetadata Metadata => new()
    {
        Type = "github",
        DisplayName = "GitHub",
        Category = "Source Control",
        IconSvg = "<path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/>",
        Description = "GitHub repositories and issues via the GitHub REST API. Uses Personal Access Token (PAT) authentication.",
        RequiresCredentials = true,
        Format = "markdown",
        AuthBuilder = "bearer",
        FieldSchema =
        [
            new ConnectorFieldDescriptor
            {
                Key = "url",
                Label = "GitHub API URL",
                FieldType = "url",
                Placeholder = "https://api.github.com/repos/{owner}/{repo}/contents/{path}",
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
                Key = "pat",
                Label = "Mã truy cập (PAT)",
                FieldType = "password",
                Required = true,
                HelpUrl = "https://github.com/settings/tokens"
            },
            new ConnectorFieldDescriptor
            {
                Key = "owner",
                Label = "Tên chủ sở hữu",
                FieldType = "text",
                Placeholder = "your-org-or-username",
                Required = true
            },
            new ConnectorFieldDescriptor
            {
                Key = "repo",
                Label = "Tên kho lưu trữ",
                FieldType = "text",
                Placeholder = "your-repository",
                Required = true
            },
        ]
    };

    /// <summary>
    /// Executes the GitHub connector by delegating to the underlying <see cref="HttpConnector"/>.
    /// </summary>
    /// <param name="ctx">Connector execution context containing configuration and credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connector result.</returns>
    public Task<ConnectorResult> ExecuteAsync(ConnectorContext ctx, CancellationToken ct)
        => inner.ExecuteAsync(ctx, ct);

    /// <summary>
    /// Tests the GitHub connection by delegating to the underlying <see cref="HttpConnector"/>.
    /// </summary>
    /// <param name="ctx">Connector execution context containing configuration and credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the connection is reachable and authenticated; otherwise <see langword="false"/>.</returns>
    public Task<bool> TestConnectionAsync(ConnectorContext ctx, CancellationToken ct)
        => inner.TestConnectionAsync(ctx, ct);

    /// <summary>
    /// Returns the JSON configuration schema by delegating to the underlying <see cref="HttpConnector"/>.
    /// </summary>
    /// <returns>A <see cref="System.Text.Json.JsonElement"/> describing the accepted configuration fields.</returns>
    public System.Text.Json.JsonElement GetConfigSchema()
        => inner.GetConfigSchema();

    /// <inheritdoc/>
    public async Task<ConnectorBrowseResult?> ListDocumentsAsync(
        ConnectorContext context,
        ConnectorBrowseQuery query,
        CancellationToken ct)
    {
        // Derive GitHub API base from config url — always api.github.com.
        string? configUrl = context.Config.RootElement
            .TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(configUrl)) return null;

        // Always use api.github.com as the search base (same pattern as Notion using api.notion.com).
        Uri configUri = new(configUrl);
        string apiBase = $"{configUri.Scheme}://{configUri.Host}";

        // Determine owner/repo from query.Scope or parse from config URL path.
        // Config URL form: https://api.github.com/repos/{owner}/{repo}/contents/{path}
        string? ownerRepo = query.Scope;
        if (string.IsNullOrWhiteSpace(ownerRepo))
        {
            string[] segments = configUri.AbsolutePath.Trim('/').Split('/');
            // path: repos/{owner}/{repo}/contents/...
            if (segments.Length >= 3 && segments[0].Equals("repos", StringComparison.OrdinalIgnoreCase))
                ownerRepo = $"{segments[1]}/{segments[2]}";
        }

        if (string.IsNullOrWhiteSpace(ownerRepo)) return null;

        // Build /search/code query server-side — BA never sees the raw query string.
        string repoFilter = $"repo:{ownerRepo}";
        string qParam = query.SearchText is { Length: > 0 } q
            ? $"{Uri.EscapeDataString(q)}+{Uri.EscapeDataString(repoFilter)}"
            : Uri.EscapeDataString(repoFilter);

        string searchUrl = $"{apiBase}/search/code?q={qParam}&per_page={query.PageSize}";

        if (query.Cursor is { Length: > 0 } page && int.TryParse(page, out int pageNum))
            searchUrl += $"&page={pageNum}";

        (JsonDocument? doc, bool isPermissionDenied) = await inner.ReadJsonAsync(context, searchUrl, "GET", null, ct);

        if (isPermissionDenied)
            return new ConnectorBrowseResult([], null, IsPermissionDenied: true);

        if (doc is null)
            return new ConnectorBrowseResult([], null, IsPermissionDenied: false);

        try
        {
            JsonElement root = doc.RootElement;
            List<ConnectorBrowseItem> items = [];

            if (root.TryGetProperty("items", out JsonElement searchItems))
            {
                foreach (JsonElement item in searchItems.EnumerateArray())
                {
                    string? path = item.TryGetProperty("path", out JsonElement pathEl) ? pathEl.GetString() : null;
                    if (path is null) continue;

                    string? name = item.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                    string? htmlUrl = item.TryGetProperty("html_url", out JsonElement htmlEl) ? htmlEl.GetString() : null;

                    string? repoFullName = null;
                    if (item.TryGetProperty("repository", out JsonElement repo) &&
                        repo.TryGetProperty("full_name", out JsonElement fn))
                        repoFullName = fn.GetString();

                    items.Add(new ConnectorBrowseItem(
                        ExternalId: path,
                        Title: name ?? path,
                        Type: "file",
                        LastModified: null,
                        Author: null,
                        Url: htmlUrl,
                        Breadcrumb: repoFullName ?? ownerRepo));
                }
            }

            // GitHub /search/code does not return a cursor in the body — pagination via page= param.
            // We don't have total_count easily mapped to a cursor here; return null for now.
            doc.Dispose();
            return new ConnectorBrowseResult(items, null, IsPermissionDenied: false);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "[GitHubPresetConnector] ListDocumentsAsync JSON mapping failed. module=GitHubPresetConnector op=ListDocumentsAsync type=github");
            doc?.Dispose();
            return new ConnectorBrowseResult([], null, IsPermissionDenied: false);
        }
    }
}
