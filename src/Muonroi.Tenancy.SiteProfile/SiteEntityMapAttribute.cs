namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Declares an entity hierarchy mapping for source-generated EF OnModelCreating.
/// The generator emits <c>modelBuilder.Ignore&lt;CoreType&gt;()</c> and
/// <c>modelBuilder.Entity&lt;SiteType&gt;().ToTable(table)</c>.
///
/// Apply one or more of these attributes on the same ISiteProfile class alongside
/// <see cref="GenerateSiteProfileAttribute"/> to declare which entities are overridden per site.
/// This attribute alone does nothing — the source generator (Plan 02) reads it at compile time.
/// <code>
/// [GenerateSiteProfile("TCI", typeof(TciDbContext))]
/// [SiteEntityMap(typeof(CoreOrder), typeof(TciOrder), "TCI_ORDER")]
/// [SiteEntityMap(typeof(CoreProduct), typeof(TciProduct), "TCI_PRODUCT")]
/// public partial class TciSiteProfile : ISiteProfile { ... }
/// </code>
/// </summary>
/// <remarks>
/// Declares an entity hierarchy mapping for a single entity pair.
/// </remarks>
/// <param name="coreType">The base/core entity type shared across all sites.</param>
/// <param name="siteType">The site-specific entity type inheriting from <paramref name="coreType"/>.</param>
/// <param name="table">The database table name to map the site entity to.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SiteEntityMapAttribute(Type coreType, Type siteType, string table) : Attribute
{
    /// <summary>The base/core entity type shared across all sites (e.g., <c>typeof(CoreOrder)</c>).</summary>
    public Type CoreType { get; } = coreType;

    /// <summary>The site-specific entity type that inherits from the core type (e.g., <c>typeof(TciOrder)</c>).</summary>
    public Type SiteType { get; } = siteType;

    /// <summary>The database table name to map the site entity to (e.g., <c>"TCI_ORDER"</c>).</summary>
    public string Table { get; } = table;
}
