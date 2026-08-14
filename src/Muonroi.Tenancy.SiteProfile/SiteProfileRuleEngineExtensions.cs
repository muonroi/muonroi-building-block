namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Extension methods for rule engine integration with site profiles.
/// </summary>
public static class SiteProfileRuleEngineExtensions
{
    /// <summary>
    /// Register ISiteProfileFactBagEnricher for rule engine integration.
    ///
    /// Consumer calls <c>enricher.Enrich(factBag)</c> before <c>RuleOrchestrator.ExecuteAsync</c>
    /// to inject __site.id and __site.profile into the FactBag per D-11, D-12.
    ///
    /// Opt-in: only consumers that use both SiteProfile and RuleEngine need this.
    /// The SiteProfile package itself has no dependency on RuleEngine.Abstractions (D-11).
    /// </summary>
    public static IServiceCollection AddSiteProfileRuleEngineIntegration(
        this IServiceCollection services)
    {
        services.AddScoped<ISiteProfileFactBagEnricher, SiteProfileFactBagEnricher>();
        return services;
    }
}
