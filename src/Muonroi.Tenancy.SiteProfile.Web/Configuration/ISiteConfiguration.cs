namespace Muonroi.Tenancy.SiteProfile.Web.Configuration;

/// <summary>
/// Provides per-site configuration values scoped to the current request's site.
/// Values are read from appsettings.json under the "Sites:{SiteId}:*" section.
/// The current SiteId is resolved automatically via <see cref="ISiteProfileResolver"/> —
/// no manual SiteId parameter is required.
/// Supports live hot-reload when appsettings changes without application restart.
/// </summary>
public interface ISiteConfiguration
{
    /// <summary>Gets a configuration value for the current site by key.</summary>
    T? GetValue<T>(string key);

    /// <summary>Gets a configuration value for the current site, returning a default if absent.</summary>
    T GetValue<T>(string key, T defaultValue);

    /// <summary>Gets a configuration sub-section scoped to the current site.</summary>
    IConfigurationSection GetSection(string key);

    /// <summary>Gets all immediate child keys and values for the current site section.</summary>
    IEnumerable<IConfigurationSection> GetChildren();
}
