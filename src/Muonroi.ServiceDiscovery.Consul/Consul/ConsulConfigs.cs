namespace Muonroi.ServiceDiscovery.Consul.Consul;

/// <summary>
/// Configuration settings for Consul-based service discovery.
/// </summary>
public class ConsulConfigs
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "ConsulConfigs";

    /// <summary>
    /// Enables Consul integration.
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// Enables registering the service with Consul.
    /// </summary>
    public bool UseDiscovery { get; set; } = true;

    /// <summary>
    /// Optional service instance id.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Consul service name.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Consul agent address.
    /// </summary>
    public string? ConsulAddress { get; set; }

    /// <summary>
    /// Service address to register in Consul.
    /// </summary>
    public string? ServiceAddress { get; set; }

    /// <summary>
    /// Service port to register in Consul.
    /// </summary>
    public int ServicePort { get; set; }

    /// <summary>
    /// Optional Consul service metadata.
    /// </summary>
    public Dictionary<string, string>? ServiceMetadata { get; set; }
}
