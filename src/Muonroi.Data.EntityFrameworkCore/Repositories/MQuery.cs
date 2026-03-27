namespace Muonroi.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Provides a base implementation for querying entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the entity. Must inherit from <see cref="MEntity"/>.</typeparam>
public class MQuery<T> : IMQueries<T> where T : MEntity
{
    /// <summary>
    /// The base database context.
    /// </summary>
    protected readonly MDbContext DbBaseContext;
    /// <summary>
    /// The authentication information context.
    /// </summary>
    protected readonly IAuthenticateInfoContext AuthContext;
    private readonly ILicenseGuard _licenseGuard;
    /// <summary>
    /// The database set for the entity type.
    /// </summary>
    protected readonly DbSet<T> DbSet;

    /// <summary>
    /// Gets the current user ID.
    /// </summary>
    public string? CurrentUserId => AuthContext?.CurrentUserGuid;
    /// <summary>
    /// Gets the current username.
    /// </summary>
    public string? CurrentUsername => AuthContext?.CurrentUsername;

    /// <summary>
    /// Gets a queryable for the entity type, filtering out deleted entities and checking license.
    /// </summary>
    public IQueryable<T> Queryable
    {
        get
        {
            _licenseGuard.EnsureValid("db.query", typeof(T).Name);
            return DbSet.AsNoTracking().Where(m => !m.IsDeleted);
        }
    }

    /// <summary>
    /// Gets the current tenant ID.
    /// </summary>
    protected static string? TenantId => TenantContext.CurrentTenantId;

    /// <summary>
    /// Initializes a new instance of the <see cref="MQuery{T}"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="authContext">The authentication information context.</param>
    /// <param name="licenseGuard">The license guard.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="licenseGuard"/> is null.</exception>
    public MQuery(MDbContext dbContext, IAuthenticateInfoContext authContext, ILicenseGuard licenseGuard)
    {
        DbBaseContext = dbContext;
        AuthContext = authContext;
        _licenseGuard = licenseGuard ?? throw new ArgumentNullException(nameof(licenseGuard));
        DbSet = DbBaseContext.Set<T>();
    }

    /// <summary>
    /// Gets an entity by its integer ID asynchronously.
    /// </summary>
    /// <param name="id">The integer ID.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entity, or null if not found.</returns>
    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await Queryable.SingleOrDefaultAsync(c => c.Id == id).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets an entity by its GUID asynchronously.
    /// </summary>
    /// <param name="guid">The GUID.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entity, or null if not found.</returns>
    public virtual async Task<T?> GetByGuidAsync(Guid guid)
    {
        return await Queryable.SingleOrDefaultAsync(c => c.EntityId == guid).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all entities asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of entities.</returns>
    public virtual async Task<List<T>?> GetAllAsync()
    {
        return await Queryable.ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a paged list of all entities asynchronously.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the paged result of entities.</returns>
    public virtual async Task<MPagedResult<T>?> GetAllAsync(int page, int pageSize)
    {
        IOrderedQueryable<T> query = Queryable.OrderByDescending(m => m.Id);
        return await GetListPaging(query, page, pageSize).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a paged list of entities from a query asynchronously.
    /// </summary>
    /// <param name="query">The query to page.</param>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the paged result of entities.</returns>
    public async Task<MPagedResult<T>> GetListPaging(IQueryable<T> query, int page, int pageSize)
    {
        int totalItems = await query.CountAsync().ConfigureAwait(false);
        List<T> items = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync().ConfigureAwait(false);

        MPagedResult<T> paging = new()
        {
            CurrentPage = page,
            PageSize = pageSize,
            RowCount = totalItems,
            Items = items
        };
        return paging;
    }

    /// <summary>
    /// Gets entities based on a predicate asynchronously.
    /// </summary>
    /// <param name="predicate">The predicate to filter entities.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of entities.</returns>
    public virtual async Task<List<T>> GetByConditionAsync(Expression<Func<T, bool>> predicate)
    {
        return await Queryable.Where(predicate).ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if any entity exists with the specified GUID asynchronously.
    /// </summary>
    /// <param name="guid">The GUID.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if an entity exists, otherwise false.</returns>
    public async Task<bool> AnyGuidAsync(Guid guid)
    {
        return await Queryable.AnyAsync(c => c.EntityId == guid).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if any entity exists with the specified integer ID asynchronously.
    /// </summary>
    /// <param name="id">The integer ID.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if an entity exists, otherwise false.</returns>
    public virtual async Task<bool> AnyAsync(int id)
    {
        return await Queryable.AnyAsync(c => c.Id == id).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if any entity exists based on a predicate asynchronously.
    /// </summary>
    /// <param name="predicate">The predicate to filter entities.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if an entity exists, otherwise false.</returns>
    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await Queryable.AnyAsync(predicate).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts entities based on an optional predicate asynchronously.
    /// </summary>
    /// <param name="predicate">The optional predicate to filter entities.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of entities.</returns>
    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null
            ? await Queryable.CountAsync().ConfigureAwait(false)
            : await Queryable.CountAsync(predicate).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a paged list of DTOs from a query asynchronously.
    /// </summary>
    /// <typeparam name="TDto">The type of the DTO.</typeparam>
    /// <param name="query">The query to page.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="selector">The selector to map entities to DTOs.</param>
    /// <param name="keyword">The optional keyword to search for.</param>
    /// <param name="filter">The optional filter predicate.</param>
    /// <param name="orderBy">The optional order by function.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the paged result of DTOs.</returns>
    public async Task<MPagedResult<TDto>> GetPagedAsync<TDto>(
        IQueryable<T> query,
        int pageIndex,
        int pageSize,
        Expression<Func<T, TDto>> selector,
        string? keyword = null,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        where TDto : class
    {
        if (filter != null)
        {
            query = query.Where(filter);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(x => EF.Functions.Like(x.ToString(), $"%{keyword}%"));
        }

        int totalRowCount = await query.CountAsync().ConfigureAwait(false);
        if (orderBy != null)
        {
            query = orderBy(query);
        }

        List<TDto> items = await query.Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(selector)
            .ToListAsync().ConfigureAwait(false);

        MPagedResult<TDto> result = new()
        {
            CurrentPage = pageIndex,
            PageSize = pageSize,
            RowCount = totalRowCount,
            Items = items
        };
        return result;
    }

    /// <summary>
    /// Gets the first entity based on a predicate asynchronously.
    /// </summary>
    /// <param name="predicate">The predicate to filter entities.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entity, or null if not found.</returns>
    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await Queryable.FirstOrDefaultAsync(predicate).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if any entity exists based on a predicate asynchronously.
    /// </summary>
    /// <param name="predicate">The predicate to filter entities.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if an entity exists, otherwise false.</returns>
    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await Queryable.AnyAsync(predicate).ConfigureAwait(false);
    }
}
