namespace Muonroi.Tenancy.Core.Legacy;

/// <summary>
/// Legacy configuration options for multi-tenant behavior.
/// </summary>
public class MultiTenantConfigs
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "MultiTenantConfigs";

    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public static string Section => SectionName;

    /// <summary>
    /// Gets or sets a value indicating whether multi-tenant features are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether an authenticated user must include a tenant claim.
    /// </summary>
    public bool RequireTenantClaimForAuthenticatedUser { get; set; } = true;
}
