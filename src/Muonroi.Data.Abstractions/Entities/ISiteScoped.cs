namespace Muonroi.Data.Abstractions.Entities;

/// <summary>
/// Indicates that an entity is scoped to a specific site identified by a site code.
/// Used for schema-divergent multi-tenancy where multiple tenants share a site but
/// have distinct entity schemas identified by <see cref="SiteCode"/>.
/// </summary>
public interface ISiteScoped
{
    /// <summary>Gets or sets the site code that identifies the site this entity belongs to.</summary>
    string SiteCode { get; set; }
}
