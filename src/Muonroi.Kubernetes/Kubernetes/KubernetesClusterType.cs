namespace Muonroi.Kubernetes.Kubernetes;

/// <summary>
/// Supported Kubernetes cluster types.
/// </summary>
public enum KubernetesClusterType
{
    /// <summary>Upstream Kubernetes (K8s).</summary>
    K8S,
    /// <summary>K3s lightweight Kubernetes distribution.</summary>
    K3S,
    /// <summary>Amazon EKS managed Kubernetes.</summary>
    Eks
}
