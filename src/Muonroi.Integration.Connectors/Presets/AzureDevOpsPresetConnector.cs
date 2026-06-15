using System.Text.Json;
using Microsoft.Extensions.Logging;
using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Http;

namespace Muonroi.Integration.Connectors.Presets;

/// <summary>
/// Azure DevOps preset connector. Delegates 100% to <see cref="HttpConnector"/>.
/// Declares Format="markdown" and AuthBuilder="basic-pat" (empty-username Basic auth with PAT).
/// ListDocumentsAsync posts a WIQL query to /_apis/wit/wiql to browse work items.
/// </summary>
public sealed class AzureDevOpsPresetConnector(HttpConnector inner, ILogger<AzureDevOpsPresetConnector>? logger = null) : IServiceTaskConnector
{
    public ConnectorMetadata Metadata => new()
    {
        Type = "azure-devops",
        DisplayName = "Azure DevOps",
        Category = "Issue Tracker",
        IconSvg = "<path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/>",
        Description = "Azure DevOps work items and repositories. Uses Personal Access Token (PAT) authentication.",
        RequiresCredentials = true,
        Format = "markdown",
        AuthBuilder = "basic-pat",
        FieldSchema =
        [
            new ConnectorFieldDescriptor
            {
                Key = "url",
                Label = "Azure DevOps API URL",
                FieldType = "url",
                Placeholder = "https://dev.azure.com/{org}/{project}/_apis/wit/workitems",
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
                Key = "orgUrl",
                Label = "Địa chỉ tổ chức",
                FieldType = "url",
                Placeholder = "https://dev.azure.com/your-org",
                Required = true
            },
            new ConnectorFieldDescriptor
            {
                Key = "pat",
                Label = "Mã truy cập (PAT)",
                FieldType = "password",
                Required = true,
                HelpUrl = "https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate"
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
        // Derive org URL from config — extract https://{host}/{org} prefix.
        string? configUrl = context.Config.RootElement
            .TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(configUrl)) return null;

        // Parse org URL: strip /_apis/... path to get https://dev.azure.com/{org}
        string orgUrl = configUrl.TrimEnd('/');
        int apisIdx = orgUrl.IndexOf("/_apis/", StringComparison.OrdinalIgnoreCase);
        if (apisIdx > 0) orgUrl = orgUrl[..apisIdx];

        // Determine team project scope — from query.Scope or parsed from config url.
        string? project = query.Scope;
        if (string.IsNullOrWhiteSpace(project))
        {
            // Try to parse project from config URL path segment after org.
            // e.g. https://dev.azure.com/org/project/_apis/... → project
            Uri configUri = new(configUrl);
            string[] segments = configUri.AbsolutePath.Trim('/').Split('/');
            // dev.azure.com path: segments[0]=org, segments[1]=project
            // for hosted: segments[1] is the project name when > 1 segment
            if (segments.Length > 1) project = segments[1];
        }

        // Build WIQL query server-side — BA never sees it (BROWSE-01).
        // SELECT [System.Id],[System.Title] FROM WorkItems WHERE ...
        System.Text.StringBuilder wiqlSb = new(
            "SELECT [System.Id],[System.Title],[System.ChangedDate] FROM WorkItems");

        List<string> whereClauses = [];

        if (!string.IsNullOrWhiteSpace(project))
            whereClauses.Add($"[System.TeamProject] = '{project.Replace("'", "''")}'");

        if (query.SearchText is { Length: > 0 } q)
            whereClauses.Add($"[System.Title] CONTAINS '{q.Replace("'", "''")}'");

        if (whereClauses.Count > 0)
        {
            wiqlSb.Append(" WHERE ");
            wiqlSb.Append(string.Join(" AND ", whereClauses));
        }

        wiqlSb.Append(" ORDER BY [System.ChangedDate] DESC");

        string wiql = wiqlSb.ToString();
        string wiqlBody = System.Text.Json.JsonSerializer.Serialize(new { query = wiql });
        string wiqlUrl = $"{orgUrl}/_apis/wit/wiql?api-version=7.1";

        (JsonDocument? doc, bool isPermissionDenied) = await inner.ReadJsonAsync(context, wiqlUrl, "POST", wiqlBody, ct);

        if (isPermissionDenied)
            return new ConnectorBrowseResult([], null, IsPermissionDenied: true);

        if (doc is null)
            return new ConnectorBrowseResult([], null, IsPermissionDenied: false);

        try
        {
            JsonElement root = doc.RootElement;
            List<ConnectorBrowseItem> items = [];

            if (root.TryGetProperty("workItems", out JsonElement workItems))
            {
                foreach (JsonElement wi in workItems.EnumerateArray())
                {
                    int? id = wi.TryGetProperty("id", out JsonElement idEl) && idEl.TryGetInt32(out int parsedId)
                        ? parsedId : null;
                    if (id is null) continue;

                    string? itemUrl = wi.TryGetProperty("url", out JsonElement itemUrlEl) ? itemUrlEl.GetString() : null;

                    items.Add(new ConnectorBrowseItem(
                        ExternalId: id.Value.ToString(),
                        Title: $"Work Item #{id}",
                        Type: "work-item",
                        LastModified: null,
                        Author: null,
                        Url: itemUrl,
                        Breadcrumb: project));
                }
            }

            doc.Dispose();
            return new ConnectorBrowseResult(items, null, IsPermissionDenied: false);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "[AzureDevOpsPresetConnector] ListDocumentsAsync JSON mapping failed. module=AzureDevOpsPresetConnector op=ListDocumentsAsync type=azure-devops");
            doc?.Dispose();
            return new ConnectorBrowseResult([], null, IsPermissionDenied: false);
        }
    }
}
