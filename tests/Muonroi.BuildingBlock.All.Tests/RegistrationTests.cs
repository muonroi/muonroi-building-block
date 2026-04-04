
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Muonroi.AspNetCore.Extensions;
using Xunit;
using Moq;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Governance.License;
using Muonroi.Governance.Abstractions.License;

namespace Muonroi.BuildingBlock.All.Tests;

public class RegistrationTests
{
    [Fact]
    public void AddInfrastructure_ShouldRegisterCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configDict = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:",
            ["LicenseConfigs:Tier"] = "Enterprise",
            ["LicenseConfigs:ProjectSeed"] = "A_VERY_LONG_PROJECT_SEED_FOR_TESTING"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
        
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(x => x.EnvironmentName).Returns("Development");
        envMock.Setup(x => x.ContentRootPath).Returns(AppContext.BaseDirectory);

        services.AddSingleton(envMock.Object);
        services.AddSingleton<IHostEnvironment>(envMock.Object);
        
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        // Register a valid Enterprise license state to bypass license checks during registration
        services.AddSingleton(new LicenseState 
        { 
            IsValid = true, 
            Tier = LicenseTier.Enterprise,
            Payload = new LicensePayload { AllowedFeatures = new[] { "*" } }
        });

        // Act
        var assembly = Assembly.GetExecutingAssembly();
        services.AddInfrastructure(config, null, null, true, "", assembly);
        
        var provider = services.BuildServiceProvider();

        // Assert
        // Check core Muonroi services instead of Mediator/Mapper which are registered differently
        Assert.NotNull(provider.GetService<ISystemExecutionContextAccessor>());
        Assert.NotNull(provider.GetService<ILicenseGuard>());
    }
}
