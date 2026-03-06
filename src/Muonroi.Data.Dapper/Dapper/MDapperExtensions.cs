using Muonroi.Data.Dapper.Dapper;

namespace Muonroi.Data.Dapper.Dapper;

public static class MDapperExtensions
{
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
