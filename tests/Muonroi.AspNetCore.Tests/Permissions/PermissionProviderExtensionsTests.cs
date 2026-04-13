using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.AspNetCore.Extensions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionProviderExtensionsTests
{
    private class SampleProvider : IPermissionProvider
    {
        public IEnumerable<PermissionDefinition> GetPermissions()
        {
            return [];
        }
    }

    [Fact]
    public void AddPermissionProviders_Registers_Providers()
    {
        ServiceCollection services = [];
        services.AddPermissionProviders(typeof(SampleProvider).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();
        IPermissionProvider[] p = [.. provider.GetServices<IPermissionProvider>()];
        Assert.Contains(p, provider => provider is SampleProvider);
    }

    [Fact]
    public void AddPermissionProviders_Can_Be_Called_Twice()
    {
        ServiceCollection services = [];
        services.AddPermissionProviders(typeof(SampleProvider).Assembly);
        services.AddPermissionProviders(typeof(SampleProvider).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();
        int count = provider.GetServices<IPermissionProvider>().Count(p => p is SampleProvider);
        Assert.Equal(2, count);
    }
}
