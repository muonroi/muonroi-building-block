namespace Muonroi.Data.EntityFrameworkCore.Repositories;

public class MQuery<T> : IMQueries<T> where T : MEntity
{
    protected readonly MDbContext DbBaseContext;
    protected readonly IAuthenticateInfoContext AuthContext;
    private readonly ILicenseGuard _licenseGuard;
    protected readonly DbSet<T> DbSet;

    public string? CurrentUserId => AuthContext?.CurrentUserGuid;
    public string? CurrentUsername => AuthContext?.CurrentUsername;

    public IQueryable<T> Queryable
    {
        get
        {
            _licenseGuard.EnsureValid("db.query", typeof(T).Name);
            return DbSet.AsNoTracking().Where(m => !m.IsDeleted);
        }
    }

    protected static string? TenantId => TenantContext.CurrentTenantId;

    public MQuery(MDbContext dbContext, IAuthenticateInfoContext authContext, ILicenseGuard licenseGuard)
    {
        DbBaseContext = dbContext;
        AuthContext = authContext;
        _licenseGuard = licenseGuard ?? throw new ArgumentNullException(nameof(licenseGuard));
        DbSet = DbBaseContext.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await Queryable.SingleOrDefaultAsync(c => c.Id == id).ConfigureAwait(false);
    }

    public virtual async Task<T?> GetByGuidAsync(Guid guid)
    {
        return await Queryable.SingleOrDefaultAsync(c => c.EntityId == guid).ConfigureAwait(false);
    }

    public virtual async Task<List<T>?> GetAllAsync()
    {
        return await Queryable.ToListAsync().ConfigureAwait(false);
    }

    public virtual async Task<MPagedResult<T>?> GetAllAsync(int page, int pageSize)
    {
        IOrderedQueryable<T> query = Queryable.OrderByDescending(m => m.Id);
        return await GetListPaging(query, page, pageSize).ConfigureAwait(false);
    }


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

    public virtual async Task<List<T>> GetByConditionAsync(Expression<Func<T, bool>> predicate)
    {
        return await Queryable.Where(predicate).ToListAsync().ConfigureAwait(false);
    }

    public async Task<bool> AnyGuidAsync(Guid guid)
    {
        return await Queryable.AnyAsync(c => c.EntityId == guid).ConfigureAwait(false);
    }

    public virtual async Task<bool> AnyAsync(int id)
    {
        return await Queryable.AnyAsync(c => c.Id == id).ConfigureAwait(false);
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await Queryable.AnyAsync(predicate).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null
            ? await Queryable.CountAsync().ConfigureAwait(false)
            : await Queryable.CountAsync(predicate).ConfigureAwait(false);
    }

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

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await Queryable.FirstOrDefaultAsync(predicate).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await Queryable.AnyAsync(predicate).ConfigureAwait(false);
    }
}
