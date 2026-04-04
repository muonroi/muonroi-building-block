namespace Muonroi.Tenancy.SiteProfile.Web.HotReload;

/// <summary>
/// Handles site profile change events from Control Plane.
/// </summary>
public interface ISiteProfileChangeHandler
{
    Task OnSiteProfileChangedAsync(SiteProfileChangeEvent evt, CancellationToken cancellationToken);
}

/// <summary>
/// Event published by Control Plane when a site profile is enabled/disabled.
/// </summary>
public sealed record SiteProfileChangeEvent(string SiteId, bool IsEnabled);
