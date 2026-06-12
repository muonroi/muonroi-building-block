using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Muonroi.Tenancy.SiteProfile.Tests;

public class SiteProfileFactBagEnricherTests
{
    private class MockProfile : ISiteProfile
    {
        public string SiteId => "TCI";
        public void RegisterServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration) { }
    }

    [Fact]
    public void Enrich_InjectsCorrectSiteId()
    {
        var profile = new MockProfile();
        var resolver = Substitute.For<ISiteProfileResolver>();
        resolver.Current.Returns(profile);

        var enricher = new SiteProfileFactBagEnricher(resolver);
        var factBag = new Dictionary<string, object?>();

        enricher.Enrich(factBag);

        factBag[SiteProfileFactBagEnricher.SiteIdKey].Should().Be("TCI");
    }

    [Fact]
    public void Enrich_InjectsCorrectProfileTypeName()
    {
        var profile = new MockProfile();
        var resolver = Substitute.For<ISiteProfileResolver>();
        resolver.Current.Returns(profile);

        var enricher = new SiteProfileFactBagEnricher(resolver);
        var factBag = new Dictionary<string, object?>();

        enricher.Enrich(factBag);

        factBag[SiteProfileFactBagEnricher.SiteProfileKey].Should().Be(nameof(MockProfile));
    }

    [Fact]
    public void Enrich_OverwritesExistingKeys()
    {
        var profile = new MockProfile();
        var resolver = Substitute.For<ISiteProfileResolver>();
        resolver.Current.Returns(profile);

        var enricher = new SiteProfileFactBagEnricher(resolver);
        var factBag = new Dictionary<string, object?>
        {
            [SiteProfileFactBagEnricher.SiteIdKey] = "OLD-ID",
            ["OtherKey"] = "Val"
        };

        enricher.Enrich(factBag);

        factBag[SiteProfileFactBagEnricher.SiteIdKey].Should().Be("TCI");
        factBag["OtherKey"].Should().Be("Val");
    }
}
