namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Registry for discovering and resolving connectors by type.
/// </summary>
public interface IConnectorRegistry
{
    /// <summary>Resolves a connector implementation by type.</summary>
    /// <param name="connectorType">Connector type key.</param>
    IServiceTaskConnector? Resolve(string connectorType);
    /// <summary>Lists all available connector metadata.</summary>
    IReadOnlyList<ConnectorMetadata> ListAvailable();
}
