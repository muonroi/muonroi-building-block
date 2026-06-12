using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.SignalR.SignalR;
using Xunit;

namespace Muonroi.SignalR.Tests;

public class MUiEngineHubTests
{
    [Fact]
    public void MSchemaWatcherGroup_Should_Have_Expected_Name()
    {
        MUiEngineHub.MSchemaWatcherGroup.Should().Be("mui-engine-schema-watchers");
    }
}

public class SignalRServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSignalRWithTenant_Without_MultiTenant_Should_Not_Register_TenantHubFilter()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> configDict = new()
        {
            ["MultiTenantConfigs:Enabled"] = "false"
        };
        IConfiguration config =
            new ConfigurationBuilder()
                .AddInMemoryCollection(configDict)
                .Build();

        services.AddSignalRWithTenant(config);
        ServiceProvider provider = services.BuildServiceProvider();

        // TenantHubFilter should NOT be registered when multi-tenant is disabled
        // IHubFilter won't be resolvable unless explicitly registered
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Microsoft.AspNetCore.SignalR.IHubFilter));
        descriptor.Should().BeNull();
    }
}
