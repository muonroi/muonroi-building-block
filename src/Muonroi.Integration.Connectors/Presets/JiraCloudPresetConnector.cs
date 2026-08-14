namespace Muonroi.Integration.Connectors.Presets;

/// <summary>
/// Jira Cloud preset connector. Delegates 100% to <see cref="HttpConnector"/>.
/// Declares Format="adf" so the format-registry normalizer picks the ADF normalizer.
/// IMPORTANT: Jira Cloud API v3 only — Jira Server / Data Center is not supported.
/// </summary>
public sealed class JiraCloudPresetConnector(HttpConnector inner, ILogger<JiraCloudPresetConnector>? logger = null) : IServiceTaskConnector
{
    /// <summary>
    /// Connector metadata describing Jira Cloud capabilities and configuration schema.
    /// </summary>
    public ConnectorMetadata Metadata => new()
    {
        Type = "jira-cloud",
        DisplayName = "Jira Cloud",
        Category = "Issue Tracker",
        IconSvg = "<path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/>",
        Description = "Jira Cloud API v3 only. Jira Server / Data Center is not supported.",
        RequiresCredentials = true,
        Format = "adf",
        AuthBuilder = "basic-email",
        FieldSchema =
        [
            new ConnectorFieldDescriptor
            {
                Key = "url",
                Label = "Jira Base URL",
                FieldType = "url",
                Placeholder = "https://org.atlassian.net/rest/api/3/issue",
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
                Key = "email",
                Label = "Email đăng nhập",
                FieldType = "text",
                Required = true
            },
            new ConnectorFieldDescriptor
            {
                Key = "apiToken",
                Label = "Mã truy cập (API token)",
                FieldType = "password",
                Required = true,
                HelpUrl = "https://id.atlassian.com/manage-profile/security/api-tokens"
            },
        ]
    };

    /// <summary>
    /// Executes the Jira Cloud connector by delegating to the underlying <see cref="HttpConnector"/>.
    /// </summary>
    /// <param name="ctx">Connector execution context containing configuration and credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connector result.</returns>
    public Task<ConnectorResult> ExecuteAsync(ConnectorContext ctx, CancellationToken ct)
        => inner.ExecuteAsync(ctx, ct);

    /// <summary>
    /// Tests the Jira Cloud connection by delegating to the underlying <see cref="HttpConnector"/>.
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
        // Derive base URL from config — same pattern as BuildConfigWithSourceRef.
        string? baseUrl = context.Config.RootElement
            .TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;

        // Extract the Jira root (strip any /issue path suffix if present).
        string jiraRoot = baseUrl.TrimEnd('/');
        int issueIdx = jiraRoot.IndexOf("/rest/api/3/issue", StringComparison.OrdinalIgnoreCase);
        if (issueIdx > 0) jiraRoot = jiraRoot[..issueIdx];

        // Build JQL server-side — BA never sees it (BROWSE-01).
        string jql = query.SearchText is { Length: > 0 } q
            ? $"text ~ \"{q.Replace("\"", "\\\"")}\" ORDER BY updated DESC"
            : "ORDER BY updated DESC";

        if (query.Scope is { Length: > 0 } project)
            jql = $"project = {project} AND {jql}";

        int startAt = 0;
        if (query.Cursor is { Length: > 0 } cursor && int.TryParse(cursor, out int parsed))
            startAt = parsed;

        string searchUrl = $"{jiraRoot}/rest/api/3/search?jql={Uri.EscapeDataString(jql)}&startAt={startAt}&maxResults={query.PageSize}";

        (JsonDocument? doc, bool isPermissionDenied) = await inner.ReadJsonAsync(context, searchUrl, "GET", null, ct);

        if (isPermissionDenied)
            return new ConnectorBrowseResult([], null, IsPermissionDenied: true);

        if (doc is null)
            return new ConnectorBrowseResult([], null, IsPermissionDenied: false);

        try
        {
            JsonElement root = doc.RootElement;
            List<ConnectorBrowseItem> items = [];

            if (root.TryGetProperty("issues", out JsonElement issues))
            {
                foreach (JsonElement issue in issues.EnumerateArray())
                {
                    string? key = issue.TryGetProperty("key", out JsonElement keyEl) ? keyEl.GetString() : null;
                    if (key is null) continue;

                    JsonElement fields = issue.TryGetProperty("fields", out JsonElement f) ? f : default;

                    string? summary = fields.ValueKind == JsonValueKind.Object &&
                        fields.TryGetProperty("summary", out JsonElement s) ? s.GetString() : null;

                    DateTimeOffset? updated = null;
                    if (fields.ValueKind == JsonValueKind.Object &&
                        fields.TryGetProperty("updated", out JsonElement u) &&
                        DateTimeOffset.TryParse(u.GetString(), out DateTimeOffset parsedDate))
                        updated = parsedDate;

                    string? author = null;
                    if (fields.ValueKind == JsonValueKind.Object &&
                        fields.TryGetProperty("creator", out JsonElement creator) &&
                        creator.TryGetProperty("displayName", out JsonElement dn))
                        author = dn.GetString();

                    string? projectName = null;
                    if (fields.ValueKind == JsonValueKind.Object &&
                        fields.TryGetProperty("project", out JsonElement proj) &&
                        proj.TryGetProperty("name", out JsonElement pn))
                        projectName = pn.GetString();

                    string itemUrl = $"{jiraRoot}/browse/{key}";

                    items.Add(new ConnectorBrowseItem(
                        ExternalId: key,
                        Title: summary ?? key,
                        Type: "issue",
                        LastModified: updated,
                        Author: author,
                        Url: itemUrl,
                        Breadcrumb: projectName));
                }
            }

            // Compute next cursor from startAt + returned count vs total.
            string? nextCursor = null;
            if (root.TryGetProperty("total", out JsonElement totalEl) &&
                totalEl.TryGetInt32(out int total) &&
                startAt + items.Count < total)
                nextCursor = (startAt + items.Count).ToString();

            doc.Dispose();
            return new ConnectorBrowseResult(items, nextCursor, IsPermissionDenied: false);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "[JiraCloudPresetConnector] ListDocumentsAsync JSON mapping failed. module=JiraCloudPresetConnector op=ListDocumentsAsync type=jira-cloud");
            doc?.Dispose();
            return new ConnectorBrowseResult([], null, IsPermissionDenied: false);
        }
    }
}
