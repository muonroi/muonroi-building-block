using Muonroi.Integration.Abstractions;

namespace Muonroi.Integration.Connectors;

/// <summary>
/// In-memory connector registry. Scans DI-registered IServiceTaskConnector instances.
/// </summary>
public sealed class DefaultConnectorRegistry : IConnectorRegistry
{
    private readonly Dictionary<string, IServiceTaskConnector> _connectors;

    public DefaultConnectorRegistry(IEnumerable<IServiceTaskConnector> connectors)
    {
        _connectors = connectors.ToDictionary(
            c => c.Metadata.Type,
            c => c,
            StringComparer.OrdinalIgnoreCase);
    }

    public IServiceTaskConnector? Resolve(string connectorType)
    {
        return _connectors.GetValueOrDefault(connectorType);
    }

    public IReadOnlyList<ConnectorMetadata> ListAvailable()
    {
        return _connectors.Values.Select(c => c.Metadata).ToList();
    }
}
