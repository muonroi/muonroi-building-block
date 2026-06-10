using System.Data;
using System.Data.Common;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Data.Dapper.Rls.Setters;

/// <summary>
/// PostgreSQL implementation of <see cref="ITenantSessionContextSetter"/>.
/// Executes <c>SET app.current_tenant_id = @tid</c> (session scope) on every connection open,
/// reusing the same GUC as the EF Core <c>TenantRlsConnectionInterceptor</c> so a single
/// set of RLS policies serves both code paths.
/// </summary>
/// <remarks>
/// <para>
/// The tenant id is bound as an ADO.NET <see cref="DbParameter"/> named <c>@tid</c> and is
/// never string-interpolated into the command text (HOOK-04 — injection safety).
/// </para>
/// <para>
/// When no tenant context is present (tenant id is <see langword="null"/> or whitespace)
/// the parameter value is sent as <see cref="string.Empty"/> so the downstream
/// PostgreSQL RLS policy fails closed (no rows returned for an empty tenant id).
/// </para>
/// </remarks>
public sealed class PostgreSqlTenantSessionContextSetter : ITenantSessionContextSetter
{
    private readonly IMLog<PostgreSqlTenantSessionContextSetter>? _log;

    /// <summary>
    /// Initializes a new instance of <see cref="PostgreSqlTenantSessionContextSetter"/>.
    /// </summary>
    /// <param name="log">
    /// Optional logger. When supplied, logs applied tenant id at Info level and warns when
    /// no tenant context is present (OBS-01).
    /// </param>
    public PostgreSqlTenantSessionContextSetter(IMLog<PostgreSqlTenantSessionContextSetter>? log = null)
    {
        _log = log;
    }

    /// <inheritdoc />
    public void Apply(IDbConnection connection, string? tenantId)
    {
        DbConnection dbConnection = (DbConnection)connection;

        using DbCommand cmd = dbConnection.CreateCommand();
        cmd.CommandText = "SET app.current_tenant_id = @tid";
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
        cmd.CommandText = "SET app.current_tenant_id = @tid";
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
