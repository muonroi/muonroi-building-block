using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Muonroi.AspNetCore.Extensions;
using Muonroi.Core.Abstractions.Models.Common;
using System.Reflection;
using Xunit;
using Muonroi.AspNetCore.Tests.Helpers;
using NSubstitute;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.AspNetCore.Tests.Extensions;

public class InfrastructureExtensionsTests
{
    private readonly IConfiguration _configuration;
    private readonly IServiceCollection _services;

    public InfrastructureExtensionsTests()
    {
        var configDict = new Dictionary<string, string?>
        {
            ["LicenseConfigs:ProjectSeed"] = "12345678901234567890",
            ["LicenseConfigs:Mode"] = "Offline",
            ["EnableEncryption"] = "false"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
        
        _services = new ServiceCollection();
        _services.AddSingleton(_configuration);
        
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");
        _services.AddSingleton(env);
        
        _services.AddLogging();
    }

    [Fact]
    public void AddInfrastructure_RegistersServices()
    {
        _services.AddInfrastructure(_configuration, new MTokenInfo(), new MPaginationConfig(), true, "secret", Assembly.GetExecutingAssembly());

        var sp = _services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<IConfiguration>());
    }

    [Fact]
    public void AddPermissionFilter_RegistersFilter()
    {
        _services.AddPermissionFilter<TestPerm>();
        // Success if no exception
    }

    [Fact]
    public void AddValidateBearerToken_RegistersAuth()
    {
        _services.AddSingleton(new MTokenInfo());
        _services.AddValidateBearerToken<TestDbContext, TestPerm>(_configuration);

        var sp = _services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ITokenSigner>());
    }
}
