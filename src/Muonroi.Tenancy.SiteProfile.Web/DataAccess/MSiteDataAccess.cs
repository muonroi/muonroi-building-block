namespace Muonroi.Tenancy.SiteProfile.Web.DataAccess;

/// <summary>
/// Unified data access facade combining EF Core writes and Dapper reads through a single injectable dependency.
/// Domain events dispatch automatically via MDbContext.SaveChangesAsync() — no explicit event publishing needed (D-05).
/// </summary>
/// <typeparam name="TContext">The per-site DbContext type. Must inherit from MDbContext.</typeparam>
internal sealed class MSiteDataAccess<TContext>(
    TContext context,
    ISiteQueryExecutor queryExecutor,
    SiteSqlBuilder sqlBuilder) : IMSiteDataAccess<TContext>
    where TContext : MDbContext
{
    private readonly TContext _context = MGuard.NotNull(context);
    private readonly ISiteQueryExecutor _queryExecutor = MGuard.NotNull(queryExecutor);
    private readonly SiteSqlBuilder _sqlBuilder = MGuard.NotNull(sqlBuilder);

    // --- Escape hatches (D-04) ---
    public TContext Context => _context;
    public SiteSqlBuilder Sql => _sqlBuilder;

    // --- Write surface (D-02) ---
    public async Task WriteAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await _context.Set<T>().AddAsync(entity, ct);
    }

    public async Task WriteManyAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class
    {
        await _context.Set<T>().AddRangeAsync(entities, ct);
    }

    public Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        _context.Set<T>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        _context.Set<T>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // MDbContext.SaveChangesAsync dispatches domain events via IMediator automatically (D-05)
        return await _context.SaveChangesAsync(ct);
    }

    // --- Read surface (D-03) ---
    public async Task<IEnumerable<TResult>> QueryAsync<TResult>(
        string markerSql, object? param = null, CancellationToken ct = default)
    {
        return await _queryExecutor.QueryAsync<TResult>(markerSql, param, ct);
    }

    public async Task<TResult?> QueryFirstOrDefaultAsync<TResult>(
        string markerSql, object? param = null, CancellationToken ct = default)
    {
        return await _queryExecutor.QueryFirstOrDefaultAsync<TResult>(markerSql, param, ct);
    }

    public async Task<int> ExecuteAsync(
        string markerSql, object? param = null, CancellationToken ct = default)
    {
        return await _queryExecutor.ExecuteAsync(markerSql, param, ct);
    }
}
