namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Enriches a FactBag (IDictionary&lt;string, object?&gt;) with site profile context
/// before rule orchestration. Consumer wires via AddSiteProfileRuleEngineIntegration().
///
/// Well-known keys injected:
/// <list type="bullet">
///   <item><term>__site.id</term><description>string — current ISiteProfile.SiteId (e.g., "TCI")</description></item>
///   <item><term>__site.profile</term><description>string — ISiteProfile implementation type name</description></item>
/// </list>
///
/// Rules can branch on site context: <c>__site.id == "TCI"</c> in conditions per D-13.
/// </summary>
public interface ISiteProfileFactBagEnricher
{
    /// <summary>
    /// Inject site profile facts into the FactBag dictionary.
    /// Call before RuleOrchestrator.ExecuteAsync to enable site-aware rule branching.
    /// </summary>
    /// <param name="factBag">The FactBag dictionary to enrich.</param>
    void Enrich(IDictionary<string, object?> factBag);
}
