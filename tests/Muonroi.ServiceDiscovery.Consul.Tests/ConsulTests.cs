namespace Muonroi.ServiceDiscovery.Consul.Tests;

public class ConsulTests
{
    [Fact]
    public void AddServiceDiscovery_ShouldRegisterConsulClient_InProduction()
    {
        // Arrange
        var services = new ServiceCollection();
        var configDict = new Dictionary<string, string?>
        {
            ["ConsulConfigs:Enable"] = "true",
            ["ConsulConfigs:UseDiscovery"] = "true",
            ["ConsulConfigs:ServiceName"] = "TestService",
            ["ConsulConfigs:ConsulAddress"] = "http://localhost:8500"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
        
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(x => x.EnvironmentName).Returns("Production");

        // Act
        services.AddServiceDiscovery(config, envMock.Object);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<IConsulClient>());
    }
}
