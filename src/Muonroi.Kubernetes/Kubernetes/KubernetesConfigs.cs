namespace Muonroi.Kubernetes.Kubernetes;

public class KubernetesConfigs
{
    public const string SectionName = "KubernetesConfigs";
    public KubernetesClusterType ClusterType { get; set; } = KubernetesClusterType.K8S;
    public string? ClusterEndpoint { get; set; }
}
