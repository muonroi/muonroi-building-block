using System.Collections.Concurrent;

namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Thread-safe singleton implementation of ISiteProfileStateRegistry.
/// Uses ConcurrentDictionary for lock-free reads and writes.
/// </summary>
public sealed class SiteProfileStateRegistry : ISiteProfileStateRegistry
{
    private readonly ConcurrentDictionary<string, bool> _states = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool? IsSiteEnabled(string siteId)
    {
        return _states.TryGetValue(siteId, out var enabled) ? enabled : null;
    }

    /// <inheritdoc />
    public void SetSiteEnabled(string siteId, bool isEnabled)
    {
        _states[siteId] = isEnabled;
    }
}
