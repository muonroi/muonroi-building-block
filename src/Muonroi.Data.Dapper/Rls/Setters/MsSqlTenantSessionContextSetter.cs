using System.Data;
using System.Data.Common;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Data.Dapper.Rls.Setters;

/// <summary>
/// MSSQL implementation of <see cref="ITenantSessionContextSetter"/>.
/// Executes <c>EXEC sp_set_session_context @key=N'TenantId', @value=@tid, @read_only=1</c>
/// on every connection open, setting a connection-scoped session context that SQL Server
/// RLS policies read via <c>SESSION_CONTEXT(N'TenantId')</c>.
/// </summary>
/// <remarks>
/// <para>
/// The tenant id is bound as an ADO.NET <see cref="DbParameter"/> named <c>@tid</c> (mapped
/// to the <c>@value</c> positional argument of <c>sp_set_session_context</c>) and is
/// never string-interpolated into the command text (HOOK-04 — injection safety).
/// </para>
/// <para>
/// When no tenant context is present (tenant id is <see langword="null"/> or whitespace)
/// the parameter value is sent as <see cref="string.Empty"/> so the downstream
/// SQL Server RLS FILTER predicate blocks all rows for an empty tenant id.
/// </para>
/// <para>
/// Requires SQL Server 2016+ / Azure SQL. The <c>@read_only=1</c> flag prevents the
/// session context value from being overwritten before the connection returns to the pool.
/// </para>
/// </remarks>
public sealed class MsSqlTenantSessionContextSetter : ITenantSessionContextSetter
{
    private readonly IMLog<MsSqlTenantSessionContextSetter>? _log;

    /// <summary>
    /// Initializes a new instance of <see cref="MsSqlTenantSessionContextSetter"/>.
    /// </summary>
    /// <param name="log">
    /// Optional logger. When supplied, logs applied tenant id at Info level and warns when
    /// no tenant context is present (OBS-01).
    /// </param>
    public MsSqlTenantSessionContextSetter(IMLog<MsSqlTenantSessionContextSetter>? log = null)
    {
        _log = log;
    }

    /// <inheritdoc />
    public void Apply(IDbConnection connection, string? tenantId)
    {
        DbConnection dbConnection = (DbConnection)connection;

        using DbCommand cmd = dbConnection.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key=N'TenantId', @value=@tid, @read_only=1";
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@tid";
        param.Value = tenantId ?? string.Empty;
        cmd.Parameters.Add(param);
        cmd.ExecuteNonQuery();

        LogResult(tenantId);
    }

    /// <inheritdoc />
    public async Task ApplyAsync(IDbConnection connection, string? tenantId, CancellationToken ct = default)
    {
        DbConnection dbConnection = (DbConnection)connection;

        await using DbCommand cmd = dbConnection.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key=N'TenantId', @value=@tid, @read_only=1";
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@tid";
        param.Value = tenantId ?? string.Empty;
        cmd.Parameters.Add(param);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        LogResult(tenantId);
    }

    private void LogResult(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _log?.Warn("[DapperRls] RLS enabled but no tenant context present on connection open.");
        }
        else
        {
            _log?.Info("[DapperRls] Applied tenant {TenantId} to connection.", tenantId);
        }
    }
}
