using System.Text.Json;

namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Core connector contract. Each connector type (HTTP, Email, Slack, etc.)
/// implements this interface and is registered in the <see cref="IConnectorRegistry"/>.
/// </summary>
public interface IServiceTaskConnector
{
    ConnectorMetadata Metadata { get; }
    Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct);
    Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct);
    JsonElement GetConfigSchema();
}
