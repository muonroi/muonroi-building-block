namespace Muonroi.Kubernetes.Tests;

using Microsoft.Extensions.Configuration;
using Muonroi.Kubernetes.Kubernetes;
using System.Collections.Generic;
using Xunit;

public class KubernetesTests
{
    [Fact]
    public void KubernetesConfigs_ShouldBindFromConfiguration()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            ["KubernetesConfigs:ClusterType"] = "Eks",
            ["KubernetesConfigs:ClusterEndpoint"] = "http://localhost"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
        
        // Act
        var k8sConfig = new KubernetesConfigs();
        config.GetSection(KubernetesConfigs.SectionName).Bind(k8sConfig);

        // Assert
        Assert.Equal(KubernetesClusterType.Eks, k8sConfig.ClusterType);
        Assert.Equal("http://localhost", k8sConfig.ClusterEndpoint);
    }
}
