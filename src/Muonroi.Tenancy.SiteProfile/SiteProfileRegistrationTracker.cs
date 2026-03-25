namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Tracks ISiteProfile registrations and AddSiteResolvedService calls for startup validation.
/// Populated during DI setup (single-threaded), consumed read-only by SiteProfileStartupValidator at startup.
/// </summary>
internal sealed class SiteProfileRegistrationTracker
{
    private readonly HashSet<string> _siteIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Type> _resolvedServiceTypes = [];
    private bool _skipValidation;

    /// <summary>All registered site IDs.</summary>
    public IReadOnlyCollection<string> SiteIds => _siteIds;

    /// <summary>All service types registered via AddSiteResolvedService&lt;T&gt;().</summary>
    public IReadOnlyCollection<Type> ResolvedServiceTypes => _resolvedServiceTypes;

    /// <summary>Whether startup validation is disabled (e.g., for test scenarios).</summary>
    public bool SkipValidation => _skipValidation;

    /// <summary>Record a site ID discovered during profile scanning.</summary>
    public void RecordSiteId(string siteId) => _siteIds.Add(siteId);

    /// <summary>Record a service type registered via AddSiteResolvedService&lt;T&gt;().</summary>
    public void RecordResolvedServiceType(Type serviceType) => _resolvedServiceTypes.Add(serviceType);

    /// <summary>Disable startup validation. Call via SkipSiteProfileStartupValidation() extension.</summary>
    public void SetSkipValidation() => _skipValidation = true;
}
