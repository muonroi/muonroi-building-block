namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Configuration options for SiteProfile resolution behavior.
/// </summary>
public sealed class SiteProfileOptions
{
    /// <summary>
    /// When true, unknown site codes throw InvalidOperationException instead of falling back to "default".
    /// Default: false (backward compatible — falls back to "default" with a warning log).
    /// </summary>
    public bool StrictMode { get; set; }

    /// <summary>
    /// Additional assemblies to scan for ISiteProfile implementations during multi-site registration.
    /// Required when ISiteProfile implementations live in separate assemblies (D-15).
    ///
    /// <para>
    /// Usage:
    /// <code>
    /// services.AddMultiSiteProfiles(config, siteResolver, assemblies: [
    ///     typeof(DefaultProfile).Assembly,
    ///     typeof(AlphaProfile).Assembly
    /// ]);
    /// </code>
    /// </para>
    ///
    /// <para>
    /// At startup, <see cref="SiteProfileStartupValidator"/> logs a warning for any ISiteProfile
    /// implementation in these assemblies that was not discovered during registration.
    /// </para>
    /// </summary>
    public Assembly[]? SiteAssemblies { get; set; }
}
