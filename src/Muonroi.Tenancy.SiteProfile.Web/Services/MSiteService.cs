using Muonroi.Core.Abstractions.Guards;
using Dapper.Extensions;
using Muonroi.Core.Abstractions.Response;
using Muonroi.Tenancy.SiteProfile;
using Muonroi.Tenancy.SiteProfile.Web.Repositories;

namespace Muonroi.Tenancy.SiteProfile.Web.Services;

/// <summary>
/// Abstract base class that provides site-scoped service infrastructure for EF Core consumers.
///
/// <para>
/// Derive from this class to get write context, read context, site resolver, virtual mapping hooks,
/// and transaction integration without writing boilerplate plumbing:
/// <code>
/// public class OrderService : MSiteService&lt;TciOrderContext, Order&gt;
/// {
///     public OrderService(
///         TciOrderContext writeContext,
///         IMSiteRepository&lt;TciOrderContext, Order&gt; repository,
///         IDapper readContext,
///         ISiteProfileResolver siteResolver)
///         : base(writeContext, repository, readContext, siteResolver) { }
///
///     protected override Order MapCreate(Order entity)
///     {
///         entity.SiteCode = SiteResolver.Current.SiteCode;
///         return entity;
///     }
/// }
/// </code>
/// </para>
///
/// <para>
/// The <see cref="WriteContext"/> exposes the site-specific EF Core DbContext registered via
/// <see cref="SiteProfileDbContextExtensions.AddSiteDbContext{TContext}"/>.
/// The <see cref="ReadContext"/> exposes the site-resolved <see cref="IDapper"/> instance
/// registered via <see cref="SiteProfileDapperExtensions.AddSiteDapperInfrastructure"/>.
/// </para>
/// </summary>
/// <typeparam name="TContext">
/// The per-site DbContext type (e.g., <c>TciOrderContext</c>). Must inherit from <see cref="MDbContext"/>.
/// </typeparam>
/// <typeparam name="TEntity">
/// The entity type managed by this service. Must inherit from <see cref="MEntity"/>.
/// </typeparam>
public abstract class MSiteService<TContext, TEntity> : IMSiteService<TContext, TEntity>
    where TContext : MDbContext
    where TEntity : MEntity
{
    // -----------------------------------------------------------------------
    // Protected properties
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the site-specific EF Core DbContext for write operations.
    /// Resolved from DI via <see cref="SiteProfileDbContextExtensions.AddSiteDbContext{TContext}"/>.
    /// </summary>
    protected TContext WriteContext { get; }

    /// <summary>
    /// Gets the site-resolved Dapper instance for read queries.
    /// Resolved from DI via <see cref="SiteProfileDapperExtensions.AddSiteDapperInfrastructure"/>.
    /// </summary>
    protected IDapper ReadContext { get; }

    /// <summary>
    /// Gets the site profile resolver for the current request.
    /// Use <see cref="ISiteProfileResolver.Current"/> to access site metadata
    /// (e.g., <c>SiteId</c>, <c>SiteCode</c>).
    /// </summary>
    protected ISiteProfileResolver SiteResolver { get; }

    /// <summary>
    /// Gets the site-aware repository used for all CRUD and transaction operations.
    /// </summary>
    protected IMSiteRepository<TContext, TEntity> Repository { get; }

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    /// <summary>
    /// Initializes a new instance of <see cref="MSiteService{TContext,TEntity}"/>.
    /// </summary>
    /// <param name="writeContext">
    /// The per-site DbContext resolved from DI (registered via AddSiteDbContext&lt;TContext&gt;).
    /// Used for EF Core write operations and transactions.
    /// </param>
    /// <param name="repository">
    /// The site-aware repository providing CRUD operations and transaction management.
    /// </param>
    /// <param name="readContext">
    /// The site-resolved Dapper instance for raw SQL read queries.
    /// </param>
    /// <param name="siteResolver">
    /// Resolves the current site profile. Scoped — one instance per HTTP request.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is <see langword="null"/>.
    /// </exception>
    protected MSiteService(
        TContext writeContext,
        IMSiteRepository<TContext, TEntity> repository,
        IDapper readContext,
        ISiteProfileResolver siteResolver)
    {
        MGuard.NotNull(writeContext);
        MGuard.NotNull(repository);
        MGuard.NotNull(readContext);
        MGuard.NotNull(siteResolver);

        WriteContext = writeContext;
        Repository = repository;
        ReadContext = readContext;
        SiteResolver = siteResolver;
    }

    // -----------------------------------------------------------------------
    // Virtual mapping hooks
    // -----------------------------------------------------------------------

    /// <summary>
    /// Override to transform the entity before it is created.
    /// Default implementation returns the entity unchanged.
    /// </summary>
    /// <param name="entity">The entity to transform.</param>
    /// <returns>The (optionally modified) entity to persist.</returns>
    /// <example>
    /// <code>
    /// protected override Order MapCreate(Order entity)
    /// {
    ///     entity.SiteCode = SiteResolver.Current.SiteCode;
    ///     return entity;
    /// }
    /// </code>
    /// </example>
    protected virtual TEntity MapCreate(TEntity entity) => entity;

    /// <summary>
    /// Override to transform the entity before it is updated.
    /// Default implementation returns the entity unchanged.
    /// </summary>
    /// <param name="entity">The entity to transform.</param>
    /// <returns>The (optionally modified) entity to persist.</returns>
    protected virtual TEntity MapUpdate(TEntity entity) => entity;

    // -----------------------------------------------------------------------
    // IMSiteService<TContext, TEntity> implementation
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public virtual async Task<TEntity> CreateAsync(TEntity entity)
    {
        MGuard.NotNull(entity);

        TEntity mapped = MapCreate(entity);
        TEntity added = Repository.Add(mapped);
        await Repository.UnitOfWork.SaveChangesAsync().ConfigureAwait(false);
        return added;
    }

    /// <inheritdoc />
    public virtual async Task<int> UpdateAsync(TEntity entity)
    {
        MGuard.NotNull(entity);

        TEntity mapped = MapUpdate(entity);
        return await Repository.UpdateAsync(mapped).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteAsync(TEntity entity)
    {
        MGuard.NotNull(entity);

        return await Repository.DeleteAsync(entity).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task ExecuteTransactionAsync(Func<Task<MVoidMethodResult>> action)
    {
        MGuard.NotNull(action);

        await Repository.ExecuteTransactionAsync(action).ConfigureAwait(false);
    }
}
