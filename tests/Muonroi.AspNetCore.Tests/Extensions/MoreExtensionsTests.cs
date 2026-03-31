using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Muonroi.AspNetCore.Extensions;
using Muonroi.AspNetCore.Cors;
using Muonroi.Core.Abstractions.Interfaces;
using System.Reflection;
using Xunit;
using NSubstitute;
using Microsoft.Extensions.Logging;

namespace Muonroi.AspNetCore.Tests.Extensions;

public class FakePermissionProvider : IPermissionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() => [];
}

public class MoreExtensionsTests
{
    [Fact]
    public void AddPermissionProviders_RegistersProviders()
    {
        var services = new ServiceCollection();
        services.AddPermissionProviders(Assembly.GetExecutingAssembly());

        var sp = services.BuildServiceProvider();
        var providers = sp.GetServices<IPermissionProvider>();
        Assert.Contains(providers, p => p is FakePermissionProvider);
    }

    [Fact]
    public void AddCors_RegistersCorsWithOrigins()
    {
        var services = new ServiceCollection();
        services.AddLogging(); // Fix for ILoggerFactory
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MAllowDomains"] = "http://localhost:3000,http://example.com"
            })
            .Build();

        services.AddCors(config);

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>());
    }

    [Fact]
    public void AddCors_NoOrigins_StillRegisters()
    {
        var services = new ServiceCollection();
        services.AddLogging(); // Fix for ILoggerFactory
        var config = new ConfigurationBuilder().Build();

        services.AddCors(config);

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>());
    }
}
