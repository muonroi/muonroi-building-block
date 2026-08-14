namespace Muonroi.Tenancy.SiteProfile.Web.DataAccess;

/// <summary>
/// Internal implementation of <see cref="ISiteQueryExecutor"/>.
/// Auto-interpolates <c>[[PropertyName]]</c> markers via <see cref="SiteSqlBuilder.InterpolateMarkers"/>
/// before delegating to <see cref="IDapperRead"/> for Dapper execution.
///
/// <para>
/// This class is <c>internal sealed</c> — consumers resolve <see cref="ISiteQueryExecutor"/>
/// through DI only. Registered automatically by <c>AddSiteDataAccess&lt;TContext&gt;()</c>.
/// </para>
/// </summary>
internal sealed class SiteQueryExecutor(
    SiteSqlBuilder sqlBuilder,
    IDapperRead dapperRead) : ISiteQueryExecutor
{
    private readonly SiteSqlBuilder _sqlBuilder = sqlBuilder;
    private readonly IDapperRead _dapperRead = dapperRead;

    /// <inheritdoc />
    public async Task<IEnumerable<T>> QueryAsync<T>(string markerSql, object? param = null, CancellationToken ct = default)
    {
        MGuard.NotNull(markerSql);
        string resolvedSql = _sqlBuilder.InterpolateMarkers(markerSql);
        return await _dapperRead.QueryAsync<T>(resolvedSql, param);
    }

    /// <inheritdoc />
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string markerSql, object? param = null, CancellationToken ct = default)
    {
        MGuard.NotNull(markerSql);
        string resolvedSql = _sqlBuilder.InterpolateMarkers(markerSql);
        return await _dapperRead.QueryFirstOrDefaultAsync<T>(resolvedSql, param);
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(string markerSql, object? param = null, CancellationToken ct = default)
    {
        MGuard.NotNull(markerSql);
        string resolvedSql = _sqlBuilder.InterpolateMarkers(markerSql);
        return await _dapperRead.ExecuteAsync(resolvedSql, param);
    }
}
