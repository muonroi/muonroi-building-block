namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Default enricher — reads ISiteProfileResolver.Current and injects
/// __site.id and __site.profile into the FactBag.
///
/// Uses IDictionary&lt;string, object?&gt; (BCL) — no dependency on RuleEngine.Abstractions,
/// keeping SiteProfile decoupled from the rule engine package per D-11.
/// </summary>
internal sealed class SiteProfileFactBagEnricher(ISiteProfileResolver resolver) : ISiteProfileFactBagEnricher
{
    /// <summary>Well-known FactBag key for site identifier (e.g., "TCI").</summary>
    public const string SiteIdKey = "__site.id";

    /// <summary>Well-known FactBag key for profile implementation type name.</summary>
    public const string SiteProfileKey = "__site.profile";

    /// <inheritdoc/>
    public void Enrich(IDictionary<string, object?> factBag)
    {
        var profile = resolver.Current;
        factBag[SiteIdKey] = profile.SiteId;
        factBag[SiteProfileKey] = profile.GetType().Name;
    }
}
