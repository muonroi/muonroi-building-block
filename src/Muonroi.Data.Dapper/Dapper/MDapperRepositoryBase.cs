using Dapper;
using Muonroi.Logging.Abstractions;
using Muonroi.Tenancy.Abstractions;
using System.Data;

namespace Muonroi.Data.Dapper.Dapper;

/// <summary>
/// Base class for Dapper repositories, integrated with Muonroi ecosystem (Tenancy, Logging, Observability).
/// </summary>
/// <typeparam name="T">The repository implementation type for logging.</typeparam>
/// <param name="tenantContext">The current tenant context.</param>
/// <param name="logger">The logger instance.</param>
public abstract class MDapperRepositoryBase<T>(
    ITenantContext tenantContext,
    IMLog<T>? logger = null)
{
    protected readonly ITenantContext TenantContext = tenantContext;
    protected readonly IMLog<T>? Logger = logger;

    /// <summary>
    /// Creates an <see cref="MDapperCommand"/> and automatically populates the TenantId parameter if available.
    /// </summary>
    /// <param name="sql">The SQL command text.</param>
    /// <param name="parameters">Optional existing parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <param name="commandType">Optional command type.</param>
    /// <returns>A configured <see cref="MDapperCommand"/>.</returns>
    protected virtual MDapperCommand CreateCommand(
        string sql, 
        object? parameters = null, 
        IDbTransaction? transaction = null, 
        CommandType? commandType = null)
    {
        var dynamicParams = new DynamicParameters(parameters);
        
        // Better Together: Automatically inject TenantId into Dapper queries
        if (!string.IsNullOrWhiteSpace(TenantContext.TenantId))
        {
            if (!dynamicParams.ParameterNames.Any(x => string.Equals(x, "TenantId", StringComparison.OrdinalIgnoreCase)))
            {
                dynamicParams.Add("TenantId", TenantContext.TenantId);
            }
        }

        return new MDapperCommand
        {
            CommandText = sql,
            Parameters = dynamicParams,
            Transaction = transaction,
            CommandType = commandType
        };
    }

    /// <summary>
    /// Logs a data access error with context.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="sql">The SQL that failed.</param>
    protected void LogError(Exception ex, string sql)
    {
        Logger?.Error(ex, "[Dapper] Query failed for Tenant {TenantId}. SQL: {Sql}", 
            TenantContext.TenantId ?? "None", sql);
    }
}
