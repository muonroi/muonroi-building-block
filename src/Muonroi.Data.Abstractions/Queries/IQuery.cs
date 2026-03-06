namespace Muonroi.Data.Abstractions.Queries;

public interface IMQueries<T> where T : MEntity
{
    Task<T?> GetByIdAsync(int id);

    Task<T?> GetByGuidAsync(Guid guid);

    Task<List<T>?> GetAllAsync();

    Task<MPagedResult<T>?> GetAllAsync(int page, int pageSize);

    Task<List<T>> GetByConditionAsync(Expression<Func<T, bool>> predicate);

    Task<bool> AnyGuidAsync(Guid guid);

    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

    Task<bool> AnyAsync(int id);

    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

    Task<MPagedResult<TDto>> GetPagedAsync<TDto>(
        IQueryable<T> query,
        int pageIndex,
        int pageSize,
        Expression<Func<T, TDto>> selector,
        string? keyword = null,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        where TDto : class;

    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
}
