using System.Text.Json;

namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Core connector contract. Each connector type (HTTP, Email, Slack, etc.)
/// implements this interface and is registered in the <see cref="IConnectorRegistry"/>.
/// </summary>
public interface IServiceTaskConnector
{
    /// <summary>Connector metadata used by the registry and UI.</summary>
    ConnectorMetadata Metadata { get; }
    /// <summary>Executes the connector with the provided context.</summary>
    /// <param name="context">Execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct);
    /// <summary>Tests connectivity using the provided context.</summary>
    /// <param name="context">Execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct);
    /// <summary>Returns a JSON schema describing the connector configuration.</summary>
    JsonElement GetConfigSchema();
}
