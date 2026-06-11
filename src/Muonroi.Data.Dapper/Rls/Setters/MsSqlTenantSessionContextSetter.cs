using System.Data;
using System.Data.Common;
using Muonroi.Data.Dapper.Rls.Bypass;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Data.Dapper.Rls.Setters;

/// <summary>
/// MSSQL implementation of <see cref="ITenantSessionContextSetter"/>.
/// Executes <c>EXEC sp_set_session_context @key=N'TenantId', @value=@tid</c>
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
/// Requires SQL Server 2016+ / Azure SQL. The setter is applied on EVERY Query/Execute
/// (set-per-open) against a pooled, possibly already-set physical connection, so
/// <c>@read_only=1</c> is intentionally NOT used: re-setting a read-only session-context
/// key on the same physical session raises SQL error 15664 and would fail every
/// second-and-later command on a reused connection. Tamper-protection of the value is
/// instead delegated to the SQL Server RLS policy (the authoritative enforcement layer).
/// </para>
/// <para>
/// On every normal open, a second <c>sp_set_session_context</c> call clears the
/// <c>N'TenantBypass'</c> flag to 0. This prevents a pooled physical connection that
/// previously ran a bypass scope from leaking cross-tenant access on the next acquisition.
/// The two calls must be separate <see cref="DbCommand"/> executions —
/// <c>sp_set_session_context</c> is a stored procedure and cannot be batched via
/// semicolons in a single <see cref="DbCommand.CommandText"/>.
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

        if (DapperRlsBypass.IsActive)
        {
            using DbCommand bypassCmd = dbConnection.CreateCommand();
            bypassCmd.CommandText = "EXEC sp_set_session_context @key=N'TenantBypass', @value=@v";
            DbParameter bp = bypassCmd.CreateParameter();
            bp.ParameterName = "@v";
            bp.Value = 1;
            bypassCmd.Parameters.Add(bp);
            bypassCmd.ExecuteNonQuery();
            _log?.Warn("[DapperRls] BYPASS entered — TenantBypass=1 set in SESSION_CONTEXT. Cross-tenant access active.");
            return;
        }

        using DbCommand cmd = dbConnection.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key=N'TenantId', @value=@tid";
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@tid";
        param.Value = tenantId ?? string.Empty;
        cmd.Parameters.Add(param);
        cmd.ExecuteNonQuery();

        using DbCommand bypCmd = dbConnection.CreateCommand();
        bypCmd.CommandText = "EXEC sp_set_session_context @key=N'TenantBypass', @value=@byp";
        DbParameter bypParam = bypCmd.CreateParameter();
        bypParam.ParameterName = "@byp";
        bypParam.Value = 0;
        bypCmd.Parameters.Add(bypParam);
        bypCmd.ExecuteNonQuery();

        LogResult(tenantId);
    }

    /// <inheritdoc />
    public async Task ApplyAsync(IDbConnection connection, string? tenantId, CancellationToken ct = default)
    {
        DbConnection dbConnection = (DbConnection)connection;

        if (DapperRlsBypass.IsActive)
        {
            await using DbCommand bypassCmd = dbConnection.CreateCommand();
            bypassCmd.CommandText = "EXEC sp_set_session_context @key=N'TenantBypass', @value=@v";
            DbParameter bp = bypassCmd.CreateParameter();
            bp.ParameterName = "@v";
            bp.Value = 1;
            bypassCmd.Parameters.Add(bp);
            await bypassCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _log?.Warn("[DapperRls] BYPASS entered — TenantBypass=1 set in SESSION_CONTEXT. Cross-tenant access active.");
            return;
        }

        await using DbCommand cmd = dbConnection.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key=N'TenantId', @value=@tid";
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@tid";
        param.Value = tenantId ?? string.Empty;
        cmd.Parameters.Add(param);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await using DbCommand bypCmd = dbConnection.CreateCommand();
        bypCmd.CommandText = "EXEC sp_set_session_context @key=N'TenantBypass', @value=@byp";
        DbParameter bypParam = bypCmd.CreateParameter();
        bypParam.ParameterName = "@byp";
        bypParam.Value = 0;
        bypCmd.Parameters.Add(bypParam);
        await bypCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

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
