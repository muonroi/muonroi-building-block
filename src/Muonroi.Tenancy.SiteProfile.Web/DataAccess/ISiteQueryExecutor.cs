namespace Muonroi.Tenancy.SiteProfile.Web.DataAccess;

/// <summary>
/// Executes Dapper queries with automatic <c>[[PropertyName]]</c> marker resolution.
///
/// <para>
/// All methods accept SQL containing <c>[[PropertyName]]</c> markers. Before each Dapper call,
/// markers are auto-interpolated via <see cref="Dapper.SiteSqlBuilder.InterpolateMarkers"/> to replace
/// them with site-specific column names. This eliminates the need for callers to manually
/// resolve column names before passing SQL to Dapper.
/// </para>
///
/// Example:
/// <code>
/// // SQL with markers — column names resolved per site automatically:
/// var rows = await executor.QueryAsync&lt;OrderDto&gt;(
///     "SELECT [[BookingNo]], [[ContainerNo]] FROM ORDER_DETAIL WHERE [[SiteCode]] = @siteCode",
///     new { siteCode = "TCI" });
/// </code>
///
/// Registration: <see cref="ISiteQueryExecutor"/> is registered as scoped via
/// <c>AddSiteDataAccess&lt;TContext&gt;()</c>. The concrete implementation is
/// <see cref="SiteQueryExecutor"/> (internal — resolved through DI only).
/// </summary>
public interface ISiteQueryExecutor
{
    /// <summary>
    /// Executes a SQL query and returns a collection of results.
    /// <para>
    /// Accepts SQL with <c>[[PropertyName]]</c> markers that are auto-interpolated to site-specific
    /// column names via <c>SiteSqlBuilder.InterpolateMarkers()</c> before Dapper execution.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The result DTO type. Dapper maps column aliases to properties by name.</typeparam>
    /// <param name="markerSql">SQL with optional <c>[[PropertyName]]</c> markers. Must not be null.</param>
    /// <param name="param">Optional Dapper parameters (anonymous object or DynamicParameters).</param>
    /// <param name="ct">Cancellation token (accepted for API consistency).</param>
    /// <returns>An enumerable of <typeparamref name="T"/> instances.</returns>
    Task<IEnumerable<T>> QueryAsync<T>(string markerSql, object? param = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a SQL query and returns the first result, or <c>null</c> if no rows are found.
    /// <para>
    /// Accepts SQL with <c>[[PropertyName]]</c> markers that are auto-interpolated to site-specific
    /// column names via <c>SiteSqlBuilder.InterpolateMarkers()</c> before Dapper execution.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The result DTO type.</typeparam>
    /// <param name="markerSql">SQL with optional <c>[[PropertyName]]</c> markers. Must not be null.</param>
    /// <param name="param">Optional Dapper parameters.</param>
    /// <param name="ct">Cancellation token (accepted for API consistency).</param>
    /// <returns>The first <typeparamref name="T"/>, or <c>null</c> if no rows found.</returns>
    Task<T?> QueryFirstOrDefaultAsync<T>(string markerSql, object? param = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a non-query SQL statement (INSERT, UPDATE, DELETE) and returns the row count.
    /// <para>
    /// Accepts SQL with <c>[[PropertyName]]</c> markers that are auto-interpolated to site-specific
    /// column names via <c>SiteSqlBuilder.InterpolateMarkers()</c> before Dapper execution.
    /// </para>
    /// </summary>
    /// <param name="markerSql">SQL with optional <c>[[PropertyName]]</c> markers. Must not be null.</param>
    /// <param name="param">Optional Dapper parameters.</param>
    /// <param name="ct">Cancellation token (accepted for API consistency).</param>
    /// <returns>The number of rows affected.</returns>
    Task<int> ExecuteAsync(string markerSql, object? param = null, CancellationToken ct = default);
}
