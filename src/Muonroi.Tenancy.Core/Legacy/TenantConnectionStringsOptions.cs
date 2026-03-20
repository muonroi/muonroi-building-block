namespace Muonroi.Tenancy.Core.Legacy;

/// <summary>
/// Legacy options for tenant-specific connection strings.
/// </summary>
public class TenantConnectionStringsOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TenantConnectionStrings";

    /// <summary>
    /// Gets or sets the tenant-to-connection-string map.
    /// </summary>
    public Dictionary<string, string> ConnectionStrings { get; set; } = [];
}
