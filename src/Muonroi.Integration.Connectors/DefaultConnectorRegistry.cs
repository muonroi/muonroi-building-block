using Muonroi.Integration.Abstractions;

namespace Muonroi.Integration.Connectors;

/// <summary>
/// In-memory connector registry. Scans DI-registered IServiceTaskConnector instances.
/// </summary>
/// <remarks>
/// Creates a registry from the available connectors.
/// </remarks>
/// <param name="connectors">Connectors registered in DI.</param>
public sealed class DefaultConnectorRegistry(IEnumerable<IServiceTaskConnector> connectors) : IConnectorRegistry
{
    private readonly Dictionary<string, IServiceTaskConnector> _connectors = connectors.ToDictionary(
            c => c.Metadata.Type,
            c => c,
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves a connector by type.
    /// </summary>
    /// <param name="connectorType">Connector type key.</param>
    /// <returns>The connector instance or null.</returns>
    public IServiceTaskConnector? Resolve(string connectorType)
    {
        return _connectors.GetValueOrDefault(connectorType);
    }

    /// <summary>
    /// Lists metadata for all available connectors.
    /// </summary>
    /// <returns>Connector metadata list.</returns>
    public IReadOnlyList<ConnectorMetadata> ListAvailable()
    {
        return _connectors.Values.Select(c => c.Metadata).ToList();
    }
}
