using Muonroi.Data.Dapper.Dapper;

namespace Muonroi.Data.Dapper.Dapper;

/// <summary>
/// Provides extension methods for paging queries with Dapper.
/// </summary>
public static class MDapperExtensions
{
    /// <summary>
    /// Asynchronously executes a query and returns a list of results for a specified page.
    /// </summary>
    /// <typeparam name="TReturn">The type of the result items.</typeparam>
    /// <param name="dapper">The Dapper instance.</param>
    /// <param name="command">The command configuration.</param>
    /// <param name="pageIndex">The page index (1-based).</param>
    /// <param name="pageSite">The number of items per page.</param>
    /// <param name="enableCache">A value indicating whether to enable caching.</param>
    /// <param name="forceUpdateCache">A value indicating whether to force a cache update.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of result items for the specified page.</returns>
    public static async Task<List<TReturn>> QueryPlainPageAsync<TReturn>(
        this IDapper dapper,
        MDapperCommand command,
        int pageIndex = 1,
        int pageSite = 10,
        bool enableCache = false,
        bool forceUpdateCache = false,
        CancellationToken cancellationToken = default)
    {
        return await dapper.QueryPlainPageAsync<TReturn>(
            command.CommandText,
            pageIndex,
            pageSite,
            command.Parameters,
            null,
            enableCache,
            null,
            null,
            forceUpdateCache,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a query and returns a paged result.
    /// </summary>
    /// <typeparam name="TReturn">The type of the result items.</typeparam>
    /// <param name="dapper">The Dapper instance.</param>
    /// <param name="command">The command configuration.</param>
    /// <param name="countSql">The SQL query to count total items.</param>
    /// <param name="pageIndex">The page index (1-based).</param>
    /// <param name="pageSite">The number of items per page.</param>
    /// <param name="enableCache">A value indicating whether to enable caching.</param>
    /// <param name="forceUpdateCache">A value indicating whether to force a cache update.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paged result of items.</returns>
    public static async Task<PageResult<TReturn>> QueryPageAsync<TReturn>(
       this IDapper dapper,
       MDapperCommand command,
       string countSql,
       int pageIndex = 1,
       int pageSite = 10,
       bool enableCache = false,
       bool forceUpdateCache = false,
       CancellationToken cancellationToken = default)
    {
        return await dapper.QueryPageAsync<TReturn>
            (countSql,
            command.CommandText,
            pageIndex, pageSite,
            command.Parameters,
            null,
            enableCache,
            null,
            null,
            forceUpdateCache,
            cancellationToken);
    }
}
