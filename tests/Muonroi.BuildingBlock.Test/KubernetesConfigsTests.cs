using Muonroi.Kubernetes.Kubernetes;

namespace Muonroi.BuildingBlock.Test;

public class KubernetesConfigsTests
{
    [Fact]
    public void ClusterType_Default_Is_K8s()
    {
        KubernetesConfigs cfg = new();
        Assert.Equal(KubernetesClusterType.K8S, cfg.ClusterType);
    }

    [Fact]
    public void ClusterType_Returns_Set_Value()
    {
        KubernetesConfigs cfg = new()
        {
            ClusterType = KubernetesClusterType.Eks
        };
        Assert.Equal(KubernetesClusterType.Eks, cfg.ClusterType);
    }

    [Fact]
    public void ClusterEndpoint_Returns_Value()
    {
        KubernetesConfigs cfg = new()
        {
            ClusterEndpoint = "ep"
        };
        Assert.Equal("ep", cfg.ClusterEndpoint);
    }

    [Fact]
    public void ClusterEndpoint_Default_Null()
    {
        KubernetesConfigs cfg = new();
        Assert.Null(cfg.ClusterEndpoint);
    }
}

