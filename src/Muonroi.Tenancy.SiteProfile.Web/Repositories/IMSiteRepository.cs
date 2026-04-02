namespace Muonroi.Tenancy.SiteProfile.Web.Repositories;

/// <summary>
/// Marker interface for site-aware repositories.
/// Inherits all <see cref="IMRepository{T}"/> CRUD operations and adds a site-context type parameter
/// so consumers can register and resolve site-specific repositories via DI.
/// </summary>
/// <typeparam name="TContext">
/// The per-site DbContext type (e.g., <c>TciOrderContext</c>). Must inherit from <see cref="MDbContext"/>.
/// </typeparam>
/// <typeparam name="T">
/// The entity type. Must inherit from <see cref="MEntity"/>.
/// </typeparam>
public interface IMSiteRepository<TContext, T> : IMRepository<T>
    where TContext : MDbContext
    where T : MEntity
{
    // Marker interface — inherits all MRepository operations.
    // Consumers use this for DI registration of site-aware repositories:
    //   services.AddScoped<IMSiteRepository<TciOrderContext, Order>, OrderRepository>();
}
