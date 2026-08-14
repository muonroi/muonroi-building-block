namespace Muonroi.Integration.Connectors.Presets;

/// <summary>
/// Generic REST / JSON preset connector. Delegates 100% to <see cref="HttpConnector"/>.
/// Declares Format="json-path" so the format-registry normalizer applies dot-path extraction.
/// </summary>
public sealed class GenericRestPresetConnector(HttpConnector inner) : IServiceTaskConnector
{
    /// <summary>
    /// Connector metadata describing Generic REST capabilities and configuration schema.
    /// </summary>
    public ConnectorMetadata Metadata => new()
    {
        Type = "generic-rest",
        DisplayName = "Generic REST / JSON",
        Category = "API",
        IconSvg = "<path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/>",
        Description = "Generic REST API returning JSON. Configure a dot-path to extract the content field.",
        RequiresCredentials = false,
        Format = "json-path",
        FieldSchema =
        [
            new ConnectorFieldDescriptor
            {
                Key = "url",
                Label = "API URL",
                FieldType = "url",
                Placeholder = "https://api.example.com/content",
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
            new ConnectorFieldDescriptor
            {
                Key = "jsonPath",
                Label = "JSON Path",
                FieldType = "text",
                Placeholder = "data.content",
                Required = false
            },
        ]
    };

    /// <summary>
    /// Executes the generic REST connector by delegating to the underlying <see cref="HttpConnector"/>.
    /// </summary>
    /// <param name="ctx">Connector execution context containing configuration and credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connector result.</returns>
    public Task<ConnectorResult> ExecuteAsync(ConnectorContext ctx, CancellationToken ct)
        => inner.ExecuteAsync(ctx, ct);

    /// <summary>
    /// Tests the generic REST connection by delegating to the underlying <see cref="HttpConnector"/>.
    /// </summary>
    /// <param name="ctx">Connector execution context containing configuration and credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the connection is reachable; otherwise <see langword="false"/>.</returns>
    public Task<bool> TestConnectionAsync(ConnectorContext ctx, CancellationToken ct)
        => inner.TestConnectionAsync(ctx, ct);

    /// <summary>
    /// Returns the JSON configuration schema by delegating to the underlying <see cref="HttpConnector"/>.
    /// </summary>
    /// <returns>A <see cref="System.Text.Json.JsonElement"/> describing the accepted configuration fields.</returns>
    public System.Text.Json.JsonElement GetConfigSchema()
        => inner.GetConfigSchema();
}
