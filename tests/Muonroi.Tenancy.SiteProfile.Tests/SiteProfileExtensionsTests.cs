using Muonroi.Core.Abstractions.Exceptions;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Muonroi.Tenancy.SiteProfile.Tests;

public class SiteProfileExtensionsTests
{
    private class FakeProfile(string siteId, Action<IServiceCollection, IConfiguration>? registerAction = null) : ISiteProfile
    {
        public string SiteId => siteId;
        public void RegisterServices(IServiceCollection services, IConfiguration configuration)
            => registerAction?.Invoke(services, configuration);
    }

    private readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();

    [Fact]
    public void EmptyProfiles_RegistersResolverButNoProfiles()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "any", []);

        var sp = services.BuildServiceProvider();
        
        // Should throw because no profiles and no "default"
        var act = () => sp.GetRequiredService<ISiteProfileResolver>();
        act.Should().Throw<MInternalException>().WithMessage("*No ISiteProfile registered for site 'any'*");
    }

    [Fact]
    public void SingleProfile_ResolvesCorrectly()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var profile = new FakeProfile("TCI");
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "TCI", [profile]);

        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ISiteProfileResolver>();

        resolver.Current.SiteId.Should().Be("TCI");
    }

    [Fact]
    public void MultipleProfiles_ResolvesCorrectSiteByCode()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var p1 = new FakeProfile("TCI");
        var p2 = new FakeProfile("HNI");
        var p3 = new FakeProfile("SGN");
        
        string? siteCode = "HNI";
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => siteCode, [p1, p2, p3]);

        var sp = services.BuildServiceProvider();

        // Resolve first time
        using (var scope = sp.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
            resolver.Current.SiteId.Should().Be("HNI");
        }
        
        // Resolve again after changing site code
        siteCode = "SGN";
        using (var scope = sp.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
            resolver.Current.SiteId.Should().Be("SGN");
        }
    }

    [Fact]
    public void DuplicateSiteId_LastWins()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var p1 = new FakeProfile("TCI");
        var p2 = new FakeProfile("TCI"); // Same ID
        
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "TCI", [p1, p2]);

        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ISiteProfileResolver>();

        resolver.Current.Should().Be(p2);
    }

    [Fact]
    public void RegistrationFailure_ThrowsAggregateException()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var p1 = new FakeProfile("OK");
        var p2 = new FakeProfile("FAIL", (_, _) => throw new InvalidOperationException("BOOM"));
        
        var act = () => services.AddMultiSiteProfilesCore(_emptyConfig, _ => "OK", [p1, p2]);

        act.Should().Throw<AggregateException>()
            .WithInnerException<InvalidOperationException>().WithMessage("BOOM");
    }

    [Fact]
    public void RegistrationFailure_PartialRegistration_OtherProfilesStillRegistered()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var p1 = new FakeProfile("OK");
        var p2 = new FakeProfile("FAIL", (_, _) => throw new InvalidOperationException("BOOM"));
        
        try { services.AddMultiSiteProfilesCore(_emptyConfig, _ => "OK", [p1, p2]); }
        catch (AggregateException) { }

        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ISiteProfileResolver>();

        // OK profile should still be resolvable
        resolver.Current.SiteId.Should().Be("OK");
    }

    [Fact]
    public void Resolver_NullSiteCode_DefaultsToDefaultKey()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var pDefault = new FakeProfile("default");
        
        // Accessor returns null
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => null, [pDefault]);

        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ISiteProfileResolver>();

        resolver.Current.SiteId.Should().Be("default");
    }

    [Fact]
    public void Resolver_UnknownSite_StrictMode_ThrowsInvalidOperationException()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        services.ConfigureSiteProfile(o => o.StrictMode = true);
        
        var pDefault = new FakeProfile("default");
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "UNKNOWN", [pDefault]);

        var sp = services.BuildServiceProvider();
        
        var act = () => sp.GetRequiredService<ISiteProfileResolver>();
        act.Should().Throw<MInternalException>()
            .WithMessage("*[SITE-SAFETY]*StrictMode is enabled*");
    }

    [Fact]
    public void Resolver_UnknownSite_NonStrict_FallsBackToDefault()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        // StrictMode = false is default
        
        var pDefault = new FakeProfile("default");
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "UNKNOWN", [pDefault]);

        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ISiteProfileResolver>();

        resolver.Current.SiteId.Should().Be("default");
    }

    [Fact]
    public void Resolver_UnknownSite_NonStrict_NoDefault_ThrowsInvalidOperationException()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var p1 = new FakeProfile("TCI");
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "UNKNOWN", [p1]);

        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<ISiteProfileResolver>();
        act.Should().Throw<MInternalException>()
            .WithMessage("*No ISiteProfile registered for site 'UNKNOWN'*");
    }

    [Fact]
    public void Resolver_SiteProfileScopeOverride_TakesPrecedence()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var p1 = new FakeProfile("TCI");
        var pOverride = new FakeProfile("OVERRIDE");
        
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "TCI", [p1]);

        var sp = services.BuildServiceProvider();

        using (SiteProfileScope.ForSite(pOverride))
        {
            var resolver = sp.GetRequiredService<ISiteProfileResolver>();
            resolver.Current.SiteId.Should().Be("OVERRIDE");
        }
        
        // Back to normal — resolve in NEW scope to ensure we get fresh resolver
        using (var scope = sp.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
            resolver.Current.SiteId.Should().Be("TCI");
        }
    }

    [Fact]
    public void ConfigureSiteProfile_SetsStrictMode()
    {
        var services = new ServiceCollection();
        services.ConfigureSiteProfile(o => o.StrictMode = true);

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<SiteProfileOptions>>().Value;

        options.StrictMode.Should().BeTrue();
    }

    [Fact]
    public void NullSiteCodeAccessor_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddMultiSiteProfilesCore(_emptyConfig, null!, []);
        act.Should().Throw<MArgumentException>();
    }
}
