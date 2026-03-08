namespace Muonroi.Data.Abstractions.Queries;

/// <summary>
/// Provides a set of methods for querying entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the entity, which must inherit from <see cref="MEntity"/>.</typeparam>
public interface IMQueries<T> where T : MEntity
{
    /// <summary>
    /// Asynchronously gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entity if found; otherwise, <c>null</c>.</returns>
    Task<T?> GetByIdAsync(int id);

    /// <summary>
    /// Asynchronously gets an entity by its unique identifier (GUID).
    /// </summary>
    /// <param name="guid">The unique identifier of the entity.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entity if found; otherwise, <c>null</c>.</returns>
    Task<T?> GetByGuidAsync(Guid guid);

    /// <summary>
    /// Asynchronously gets all entities.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of all entities.</returns>
    Task<List<T>?> GetAllAsync();

    /// <summary>
    /// Asynchronously gets a paged list of all entities.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The number of entities per page.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paged result of entities.</returns>
    Task<MPagedResult<T>?> GetAllAsync(int page, int pageSize);

    /// <summary>
    /// Asynchronously gets a list of entities that satisfy a given condition.
    /// </summary>
    /// <param name="predicate">A function to test each entity for a condition.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of entities that satisfy the condition.</returns>
    Task<List<T>> GetByConditionAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Asynchronously checks if any entity exists with the specified unique identifier (GUID).
    /// </summary>
    /// <param name="guid">The unique identifier to check.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if an entity exists; otherwise, <c>false</c>.</returns>
    Task<bool> AnyGuidAsync(Guid guid);

    /// <summary>
    /// Asynchronously counts the number of entities that satisfy an optional condition.
    /// </summary>
    /// <param name="predicate">An optional function to test each entity for a condition.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of entities.</returns>
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

    /// <summary>
    /// Asynchronously checks if any entity exists with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier to check.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if an entity exists; otherwise, <c>false</c>.</returns>
    Task<bool> AnyAsync(int id);

    /// <summary>
    /// Asynchronously checks if any entity exists that satisfies a given condition.
    /// </summary>
    /// <param name="predicate">A function to test each entity for a condition.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if an entity exists; otherwise, <c>false</c>.</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Asynchronously gets a paged result for a query with optional filtering, sorting, and projection.
    /// </summary>
    /// <typeparam name="TDto">The type of the data transfer object to project into.</typeparam>
    /// <param name="query">The IQueryable query to execute.</param>
    /// <param name="pageIndex">The page index (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="selector">A function to project each entity into a DTO.</param>
    /// <param name="keyword">An optional keyword for filtering.</param>
    /// <param name="filter">An optional additional filter condition.</param>
    /// <param name="orderBy">An optional function to order the query results.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paged result of DTOs.</returns>
    Task<MPagedResult<TDto>> GetPagedAsync<TDto>(
        IQueryable<T> query,
        int pageIndex,
        int pageSize,
        Expression<Func<T, TDto>> selector,
        string? keyword = null,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        where TDto : class;

    /// <summary>
    /// Asynchronously returns the first entity that satisfies a condition, or a default value if no such entity is found.
    /// </summary>
    /// <param name="predicate">A function to test each entity for a condition.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the first entity found; otherwise, <c>null</c>.</returns>
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Asynchronously checks if any entity exists that satisfies a given condition.
    /// </summary>
    /// <param name="predicate">A function to test each entity for a condition.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if an entity exists; otherwise, <c>false</c>.</returns>
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
}
