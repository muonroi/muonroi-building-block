namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Mutable state store for site enabled/disabled status.
/// Written by SiteProfileHotReloadClient on SignalR events.
/// Read by SiteProfileStateMiddleware per-request.
/// Registered as singleton.
/// </summary>
public interface ISiteProfileStateRegistry
{
    /// <summary>
    /// Returns whether a site is enabled. If no explicit state has been set
    /// (no hot-reload event received yet), returns null — caller should
    /// fall back to ISiteProfile.IsEnabled default.
    /// </summary>
    bool? IsSiteEnabled(string siteId);

    /// <summary>
    /// Sets the enabled state for a site. Called by hot-reload client.
    /// </summary>
    void SetSiteEnabled(string siteId, bool isEnabled);
}
