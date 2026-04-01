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
}
