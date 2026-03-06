namespace Muonroi.ServiceDiscovery.Consul.Consul;

public class ConsulConfigs
{
    public const string SectionName = "ConsulConfigs";
    public bool Enable { get; set; } = true;
    public bool UseDiscovery { get; set; } = true;
    public string? Id { get; set; }
    public string? ServiceName { get; set; }
    public string? ConsulAddress { get; set; }
    public string? ServiceAddress { get; set; }
    public int ServicePort { get; set; }
    public Dictionary<string, string>? ServiceMetadata { get; set; }
}
