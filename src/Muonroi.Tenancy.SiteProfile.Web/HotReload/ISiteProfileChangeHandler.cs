namespace Muonroi.Tenancy.SiteProfile.Web.HotReload;

/// <summary>
/// Handles site profile change events from Control Plane.
/// </summary>
public interface ISiteProfileChangeHandler
{
    /// <summary>
    /// Handles a site profile change event asynchronously.
    /// </summary>
    /// <param name="evt">The event data describing the site profile change. Cannot be <c>null</c>.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnSiteProfileChangedAsync(SiteProfileChangeEvent evt, CancellationToken cancellationToken);
}

/// <summary>
/// Event published by Control Plane when a site profile is enabled/disabled.
/// </summary>
public sealed record SiteProfileChangeEvent(string SiteId, bool IsEnabled);
