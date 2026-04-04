using Muonroi.Core.Abstractions.Response;

namespace Muonroi.Tenancy.SiteProfile.Web.Services;

/// <summary>
/// Contract for a site-aware service that provides CRUD operations over a site-scoped
/// <typeparamref name="TContext"/> and entity of type <typeparamref name="TEntity"/>.
///
/// <para>
/// Implement by deriving from <see cref="MSiteService{TContext,TEntity}"/>, which supplies
/// WriteContext, ReadContext, SiteResolver,
/// virtual mapping hooks, and transaction integration out of the box.
/// </para>
/// </summary>
/// <typeparam name="TContext">
/// The per-site DbContext type (e.g., <c>TciOrderContext</c>). Must inherit from <see cref="MDbContext"/>.
/// </typeparam>
/// <typeparam name="TEntity">
/// The entity type managed by this service. Must inherit from <see cref="MEntity"/>.
/// </typeparam>
public interface IMSiteService<TContext, TEntity>
    where TContext : MDbContext
    where TEntity : MEntity
{
    /// <summary>
    /// Creates a new entity in the site-scoped write context.
    /// Implementations should invoke any <c>MapCreate</c> hook before persisting.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <returns>The entity after it has been added and changes saved.</returns>
    Task<TEntity> CreateAsync(TEntity entity);

    /// <summary>
    /// Updates an existing entity in the site-scoped write context.
    /// Implementations should invoke any <c>MapUpdate</c> hook before persisting.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>The number of rows affected.</returns>
    Task<int> UpdateAsync(TEntity entity);

    /// <summary>
    /// Soft-deletes an entity from the site-scoped write context.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <returns><see langword="true"/> if deleted; otherwise <see langword="false"/>.</returns>
    Task<bool> DeleteAsync(TEntity entity);

    /// <summary>
    /// Wraps a unit of work in a database transaction scoped to the site's write context.
    /// Delegates to the underlying <see cref="IMSiteRepository{TContext,TEntity}"/> transaction pattern.
    /// </summary>
    /// <param name="action">
    /// The operation to execute inside the transaction.
    /// Must return a <see cref="MVoidMethodResult"/> — the method commits on
    /// <c>IsOk</c> and rolls back otherwise.
    /// </param>
    Task ExecuteTransactionAsync(Func<Task<MVoidMethodResult>> action);
}
