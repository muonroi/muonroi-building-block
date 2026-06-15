using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Http;

namespace Muonroi.Integration.Connectors.Presets;

/// <summary>
/// Jira Cloud preset connector. Delegates 100% to <see cref="HttpConnector"/>.
/// Declares Format="adf" so the format-registry normalizer picks the ADF normalizer.
/// IMPORTANT: Jira Cloud API v3 only — Jira Server / Data Center is not supported.
/// </summary>
public sealed class JiraCloudPresetConnector(HttpConnector inner) : IServiceTaskConnector
{
    public ConnectorMetadata Metadata => new()
    {
        Type = "jira-cloud",
        DisplayName = "Jira Cloud",
        Category = "Issue Tracker",
        IconSvg = "<path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/>",
        Description = "Jira Cloud API v3 only. Jira Server / Data Center is not supported.",
        RequiresCredentials = true,
        Format = "adf",
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
        ]
    };

    public Task<ConnectorResult> ExecuteAsync(ConnectorContext ctx, CancellationToken ct)
        => inner.ExecuteAsync(ctx, ct);

    public Task<bool> TestConnectionAsync(ConnectorContext ctx, CancellationToken ct)
        => inner.TestConnectionAsync(ctx, ct);

    public System.Text.Json.JsonElement GetConfigSchema()
        => inner.GetConfigSchema();
}
