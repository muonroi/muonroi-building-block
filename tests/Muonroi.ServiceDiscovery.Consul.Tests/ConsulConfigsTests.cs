using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Muonroi.ServiceDiscovery.Consul.Consul;
using Xunit;

namespace Muonroi.ServiceDiscovery.Consul.Tests;

public class ConsulConfigsTests
{
    [Fact]
    public void SectionName_Should_Be_ConsulConfigs()
    {
        ConsulConfigs.SectionName.Should().Be("ConsulConfigs");
    }

    [Fact]
    public void Defaults_Should_Be_Enabled()
    {
        ConsulConfigs cfg = new();

        cfg.Enable.Should().BeTrue();
        cfg.UseDiscovery.Should().BeTrue();
        cfg.ServicePort.Should().Be(0);
        cfg.Id.Should().BeNull();
        cfg.ServiceName.Should().BeNull();
        cfg.ConsulAddress.Should().BeNull();
        cfg.ServiceAddress.Should().BeNull();
        cfg.ServiceMetadata.Should().BeNull();
    }

    [Fact]
    public void Properties_Should_Be_Settable()
    {
        ConsulConfigs cfg = new()
        {
            Enable = false,
            UseDiscovery = false,
            Id = "svc-1",
            ServiceName = "my-service",
            ConsulAddress = "http://consul:8500",
            ServiceAddress = "10.0.0.1",
            ServicePort = 5000,
            ServiceMetadata = new() { ["version"] = "1.0" }
        };

        cfg.Enable.Should().BeFalse();
        cfg.UseDiscovery.Should().BeFalse();
        cfg.Id.Should().Be("svc-1");
        cfg.ServiceName.Should().Be("my-service");
        cfg.ConsulAddress.Should().Be("http://consul:8500");
        cfg.ServiceAddress.Should().Be("10.0.0.1");
        cfg.ServicePort.Should().Be(5000);
        cfg.ServiceMetadata.Should().ContainKey("version");
    }

    [Fact]
    public void AddServiceDiscovery_Disabled_Should_Not_Register_ConsulClient()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> configDict = new()
        {
            ["ConsulConfigs:Enable"] = "false"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> envMock = new();
        envMock.Setup(x => x.EnvironmentName).Returns("Production");

        services.AddServiceDiscovery(config, envMock.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<global::Consul.IConsulClient>().Should().BeNull();
    }

    [Fact]
    public void AddServiceDiscovery_Development_Should_Not_Register_ConsulClient()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> configDict = new()
        {
            ["ConsulConfigs:Enable"] = "true",
            ["ConsulConfigs:UseDiscovery"] = "true",
            ["ConsulConfigs:ServiceName"] = "test",
            ["ConsulConfigs:ConsulAddress"] = "http://localhost:8500"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> envMock = new();
        envMock.Setup(x => x.EnvironmentName).Returns("Development");

        services.AddServiceDiscovery(config, envMock.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<global::Consul.IConsulClient>().Should().BeNull();
    }

    [Fact]
    public void AddServiceDiscovery_Missing_ServiceName_Should_Not_Register()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> configDict = new()
        {
            ["ConsulConfigs:Enable"] = "true",
            ["ConsulConfigs:UseDiscovery"] = "true",
            ["ConsulConfigs:ConsulAddress"] = "http://localhost:8500"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> envMock = new();
        envMock.Setup(x => x.EnvironmentName).Returns("Production");

        services.AddServiceDiscovery(config, envMock.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<global::Consul.IConsulClient>().Should().BeNull();
    }
}
