using System;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Marks a partial ISiteProfile class for source-generated RegisterServices() scaffolding.
/// The generator emits DbContext registration and behavior Apply() calls.
/// Consumer can add additional registrations via the generated partial void RegisterAdditionalServices().
/// <code>
/// [GenerateSiteProfile("TCI", typeof(TciOrderContext))]
/// public partial class TciSiteProfile : ISiteProfile
/// {
///     public string SiteId => "TCI";
///     // RegisterServices() is generated — add custom registrations in another partial file
/// }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateSiteProfileAttribute : Attribute
{
    /// <summary>Site identifier used as the keyed service key.</summary>
    public string SiteId { get; }

    /// <summary>DbContext type for this site (registered via MultiTenantServiceCollectionExtensions.AddDbContext).</summary>
    public Type DbContextType { get; }

    /// <summary>
    /// When true, the generated RegisterServices() skips AddDbContext registration.
    /// Use when the consumer already registers DbContext via its own infrastructure
    /// (e.g., Autofac + legacy AddInternalInfrastructure) and adding another AddDbContext
    /// causes DI container conflicts (DbContextOptions resolution ambiguity).
    /// Default: false.
    /// </summary>
    public bool SkipDbContextRegistration { get; set; }

    /// <summary>
    /// Creates a GenerateSiteProfile attribute.
    /// </summary>
    /// <param name="siteId">Site identifier string (e.g., "TCI").</param>
    /// <param name="dbContextType">DbContext type for this site.</param>
    public GenerateSiteProfileAttribute(string siteId, Type dbContextType)
    {
        SiteId = siteId;
        DbContextType = dbContextType;
    }
}
