namespace Muonroi.Kubernetes.Kubernetes;

/// <summary>
/// Configuration values for connecting to a Kubernetes cluster.
/// </summary>
public class KubernetesConfigs
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "KubernetesConfigs";
    /// <summary>Type of Kubernetes cluster.</summary>
    public KubernetesClusterType ClusterType { get; set; } = KubernetesClusterType.K8S;
    /// <summary>API server endpoint URL.</summary>
    public string? ClusterEndpoint { get; set; }
}
