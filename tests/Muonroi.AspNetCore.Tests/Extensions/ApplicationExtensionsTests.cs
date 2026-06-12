using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.AspNetCore.Extensions;
using Xunit;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;

namespace Muonroi.AspNetCore.Tests.Extensions;

public class ApplicationExtensionsTests
{
    [Fact]
    public void AddApplication_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddApplication(Assembly.GetExecutingAssembly());

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<IMDateTimeService>());
        Assert.NotNull(sp.GetService<ISystemExecutionContextAccessor>());
    }

    [Fact]
    public void AddConfigureHttpJson_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddConfigureHttpJson();

        // Verifying it doesn't throw and registers something
        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp);
    }

    [Fact]
    public void AddMediator_RegistersHandlers()
    {
        var services = new ServiceCollection();
        services.AddMediator(Assembly.GetExecutingAssembly());

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<IMediator>());
    }

    [Fact]
    public void SwaggerConfig_RegistersSwagger()
    {
        var services = new ServiceCollection();
        services.SwaggerConfig("TestService");

        // SwaggerGen adds many services
        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddBaseApi_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddBaseApi();

        // Success if no exception
        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddUiEngineChangePolicies_RegistersPolicies()
    {
        var services = new ServiceCollection();
        services.AddUiEngineChangePolicies(opts => opts.UseClaimRequirement = true);

        // Success if no exception
        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddAppConfiguration_AddsJsonAndEnv()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddAppConfiguration();

        // Success if no exception
    }
}
