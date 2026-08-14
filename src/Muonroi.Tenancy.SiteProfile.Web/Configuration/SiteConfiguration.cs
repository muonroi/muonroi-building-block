namespace Muonroi.Tenancy.SiteProfile.Web.Configuration;

/// <summary>
/// Scoped implementation of <see cref="ISiteConfiguration"/> that reads configuration
/// values from the "Sites:{SiteId}:*" section of IConfiguration.
///
/// The current SiteId is resolved on each call via <see cref="ISiteProfileResolver.Current"/> —
/// no manual SiteId parameter is required.
///
/// Hot-reload is transparent: IConfiguration is re-read on every method call
/// (no subsection caching), so updated appsettings values are visible immediately
/// when the host is configured with <c>reloadOnChange: true</c>.
///
/// Lifetime: Scoped (one instance per HTTP request, matching ISiteProfileResolver lifetime).
/// </summary>
public sealed class SiteConfiguration(ISiteProfileResolver resolver, IConfiguration configuration) : ISiteConfiguration
{
    private readonly ISiteProfileResolver _resolver = resolver;
    private readonly IConfiguration _configuration = configuration;

    /// <inheritdoc />
    public T? GetValue<T>(string key)
    {
        IConfigurationSection section = _configuration.GetSection($"Sites:{_resolver.Current.SiteId}");
        return section.GetValue<T>(key);
    }

    /// <inheritdoc />
    public T GetValue<T>(string key, T defaultValue)
    {
        IConfigurationSection section = _configuration.GetSection($"Sites:{_resolver.Current.SiteId}");
        return section.GetValue<T>(key) ?? defaultValue;
    }

    /// <inheritdoc />
    public IConfigurationSection GetSection(string key)
    {
        IConfigurationSection section = _configuration.GetSection($"Sites:{_resolver.Current.SiteId}");
        return section.GetSection(key);
    }

    /// <inheritdoc />
    public IEnumerable<IConfigurationSection> GetChildren()
    {
        IConfigurationSection section = _configuration.GetSection($"Sites:{_resolver.Current.SiteId}");
        return section.GetChildren();
    }
}
