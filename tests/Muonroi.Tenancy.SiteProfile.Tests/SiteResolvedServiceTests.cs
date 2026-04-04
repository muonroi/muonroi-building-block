using Muonroi.Core.Abstractions.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace Muonroi.Tenancy.SiteProfile.Tests;

public class SiteResolvedServiceTests
{
    private interface IMyService { string GetName(); }
    private class TciServiceImpl : IMyService { public string GetName() => "TCI"; }
    private class DefaultServiceImpl : IMyService { public string GetName() => "default"; }

    private interface IOtherService { }
    private class OtherServiceImpl : IOtherService { }

    private class FakeProfile(string siteId, Action<IServiceCollection, IConfiguration>? registerAction = null) : ISiteProfile
    {
        public string SiteId => siteId;
        public void RegisterServices(IServiceCollection services, IConfiguration configuration)
            => registerAction?.Invoke(services, configuration);
    }

    private readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();

    [Fact]
    public void ResolvesCorrectKeyedService_ForCurrentSite()
    {
        var services = new ServiceCollection();
        var profile = new FakeProfile("TCI", (s, _) => s.AddKeyedScoped<IMyService, TciServiceImpl>("TCI"));
        
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "TCI", [profile]);
        services.AddSiteResolvedService<IMyService>();
        services.SkipSiteProfileStartupValidation();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMyService>();

        service.Should().BeOfType<TciServiceImpl>();
        service.GetName().Should().Be("TCI");
    }

    [Fact]
    public void FallsBackToDefault_WhenSiteKeyMissing()
    {
        var services = new ServiceCollection();
        var profile = new FakeProfile("TCI"); // No keyed registration for TCI
        
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "TCI", [profile]);
        services.AddKeyedScoped<IMyService, DefaultServiceImpl>("default");
        services.AddSiteResolvedService<IMyService>();
        services.SkipSiteProfileStartupValidation();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMyService>();

        service.Should().BeOfType<DefaultServiceImpl>();
        service.GetName().Should().Be("default");
    }

    [Fact]
    public void ThrowsInvalidOperationException_WhenNoKeyFound()
    {
        var services = new ServiceCollection();
        var profile = new FakeProfile("TCI");
        
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "TCI", [profile]);
        services.AddSiteResolvedService<IMyService>();
        services.SkipSiteProfileStartupValidation();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        
        var act = () => scope.ServiceProvider.GetRequiredService<IMyService>();
        act.Should().Throw<MInternalException>()
            .WithMessage("*No keyed service 'IMyService' registered for site 'TCI' or 'default'*");
    }

    [Fact]
    public void MultipleServiceTypes_ResolveIndependently()
    {
        var services = new ServiceCollection();
        var profile = new FakeProfile("TCI", (s, _) => 
        {
            s.AddKeyedScoped<IMyService, TciServiceImpl>("TCI");
            s.AddKeyedScoped<IOtherService, OtherServiceImpl>("TCI");
        });
        
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "TCI", [profile]);
        services.AddSiteResolvedService<IMyService>();
        services.AddSiteResolvedService<IOtherService>();
        services.SkipSiteProfileStartupValidation();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        
        scope.ServiceProvider.GetRequiredService<IMyService>().Should().BeOfType<TciServiceImpl>();
        scope.ServiceProvider.GetRequiredService<IOtherService>().Should().BeOfType<OtherServiceImpl>();
    }
}
