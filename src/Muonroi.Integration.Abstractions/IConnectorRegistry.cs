namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Registry for discovering and resolving connectors by type.
/// </summary>
public interface IConnectorRegistry
{
    IServiceTaskConnector? Resolve(string connectorType);
    IReadOnlyList<ConnectorMetadata> ListAvailable();
}
