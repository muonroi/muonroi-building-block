namespace Muonroi.Tenancy.SiteProfile.Web.HotReload;

/// <summary>
/// Configuration for the SiteProfile hot-reload client.
/// </summary>
public sealed class SiteProfileHotReloadOptions
{
    /// <summary>
    /// Base Control Plane URL or a full site-profile-changes hub URL.
    /// </summary>
    public string? ControlPlaneUrl { get; set; }

    /// <summary>
    /// Optional bearer token factory used when the hub requires authentication.
    /// </summary>
    public Func<Task<string?>>? AccessTokenFactory { get; set; }

    /// <summary>
    /// Delay before retrying a failed initial connection.
    /// </summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(10);
}
