using System.Collections.Concurrent;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Tracks ISiteProfile registrations and AddSiteResolvedService calls for startup validation.
/// Populated during DI setup (single-threaded), consumed read-only by SiteProfileStartupValidator at startup.
/// </summary>
internal sealed class SiteProfileRegistrationTracker
{
    private readonly HashSet<string> _siteIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Type> _resolvedServiceTypes = [];
    private readonly ConcurrentBag<(string SiteId, Exception Ex)> _failures = new();
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

    /// <summary>
    /// Record a registration failure for a specific site. Used by AddMultiSiteProfiles to track
    /// partial failures so SiteProfileStartupValidator can report them after DI is built.
    /// </summary>
    public void RecordRegistrationFailure(string siteId, Exception ex) =>
        _failures.Add((siteId, ex));

    /// <summary>All registration failures recorded during profile scanning.</summary>
    public IReadOnlyList<(string SiteId, Exception Ex)> GetFailures() => _failures.ToList();

    /// <summary>Disable startup validation. Call via SkipSiteProfileStartupValidation() extension.</summary>
    public void SetSkipValidation() => _skipValidation = true;
}
