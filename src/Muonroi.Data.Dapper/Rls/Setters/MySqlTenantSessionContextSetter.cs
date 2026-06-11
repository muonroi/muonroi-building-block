using System.Data;
using System.Data.Common;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Data.Dapper.Rls.Setters;

/// <summary>
/// MySQL implementation of <see cref="ITenantSessionContextSetter"/>.
/// Executes <c>SET @app_current_tenant_id = @tid</c> on every connection open, setting a
/// session user-variable that per-table updatable views reference in their <c>WHERE</c> clause.
/// </summary>
/// <remarks>
/// <para>
/// The tenant id is bound as an ADO.NET <see cref="DbParameter"/> named <c>@tid</c> and is
/// never string-interpolated into the command text (HOOK-04 — injection safety).
/// </para>
/// <para>
/// When no tenant context is present (tenant id is <see langword="null"/> or whitespace)
/// the parameter value is sent as <see cref="string.Empty"/> so downstream views match
/// no rows for an empty tenant id.
/// </para>
/// <para>
/// MySQL has no native row-level security. Isolation is enforced by updatable views +
/// <c>WITH CHECK OPTION</c> + revoking base-table grants from the app user. This is a
/// weaker guarantee than PostgreSQL or MSSQL — anyone with direct base-table access can
/// bypass the view filter. Document this clearly and revoke base-table grants.
/// </para>
/// </remarks>
public sealed class MySqlTenantSessionContextSetter : ITenantSessionContextSetter
{
    private readonly IMLog<MySqlTenantSessionContextSetter>? _log;

    /// <summary>
    /// Initializes a new instance of <see cref="MySqlTenantSessionContextSetter"/>.
    /// </summary>
    /// <param name="log">
    /// Optional logger. When supplied, logs applied tenant id at Info level and warns when
    /// no tenant context is present (OBS-01).
    /// </param>
    public MySqlTenantSessionContextSetter(IMLog<MySqlTenantSessionContextSetter>? log = null)
    {
        _log = log;
    }

    /// <inheritdoc />
    public void Apply(IDbConnection connection, string? tenantId)
    {
        DbConnection dbConnection = (DbConnection)connection;

        using DbCommand cmd = dbConnection.CreateCommand();
        cmd.CommandText = "SET @app_current_tenant_id = @tid";
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
        cmd.CommandText = "SET @app_current_tenant_id = @tid";
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
