using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Http;

namespace Muonroi.Integration.Connectors.Presets;

/// <summary>
/// Azure DevOps preset connector. Delegates 100% to <see cref="HttpConnector"/>.
/// Declares Format="markdown" and AuthBuilder="basic-pat" (empty-username Basic auth with PAT).
/// </summary>
public sealed class AzureDevOpsPresetConnector(HttpConnector inner) : IServiceTaskConnector
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
}
