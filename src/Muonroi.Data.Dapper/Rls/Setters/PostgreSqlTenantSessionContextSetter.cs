using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Data.Dapper.Rls.Bypass;
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
    private readonly string _bypassRoleName;

    // Defense-in-depth (WR-01): the bypass role name is interpolated into "SET ROLE {name}"
    // (SET ROLE takes an identifier, not a data parameter), so even though it is sourced from
    // trusted config it must be a well-formed unquoted SQL identifier. This rejects empty,
    // whitespace, semicolons, spaces, and any other character that could yield malformed or
    // surprising SQL if config is ever sourced from a partially-trusted layer.
    private static readonly Regex SafeIdentifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of <see cref="PostgreSqlTenantSessionContextSetter"/>.
    /// </summary>
    /// <param name="bypassRoleName">
    /// The PostgreSQL role (with <c>BYPASSRLS</c>) entered via <c>SET ROLE</c> when a
    /// <c>DapperRlsBypass</c> scope is active. Must not be <see langword="null"/> or empty.
    /// </param>
    /// <param name="log">
    /// Optional logger. When supplied, logs applied tenant id at Info level, warns when
    /// no tenant context is present (OBS-01), and warns on every bypass entry (D-06).
    /// </param>
    public PostgreSqlTenantSessionContextSetter(
        string bypassRoleName,
        IMLog<PostgreSqlTenantSessionContextSetter>? log = null)
    {
        _bypassRoleName = MGuard.NotEmpty(bypassRoleName);
        if (!SafeIdentifier.IsMatch(_bypassRoleName))
        {
            throw new MArgumentException(
                nameof(bypassRoleName),
                $"BypassRoleName '{_bypassRoleName}' is not a valid PostgreSQL identifier. " +
                "It must match ^[A-Za-z_][A-Za-z0-9_]*$ (WR-01 defense-in-depth for SET ROLE).");
        }
        _log = log;
    }

    /// <inheritdoc />
    public void Apply(IDbConnection connection, string? tenantId)
    {
        DbConnection dbConnection = (DbConnection)connection;

        if (DapperRlsBypass.IsActive)
        {
            using DbCommand bypassCmd = dbConnection.CreateCommand();
            // SET ROLE takes a SQL identifier, not a data parameter. _bypassRoleName is trusted
            // config (DapperRlsOptions.BypassRoleName), never user input — see options XML doc.
            bypassCmd.CommandText = $"SET ROLE {_bypassRoleName}";
            bypassCmd.ExecuteNonQuery();
            _log?.Warn("[DapperRls] BYPASS entered — SET ROLE {BypassRole} issued. Cross-tenant access active.", _bypassRoleName);
            return;
        }

        using DbCommand cmd = dbConnection.CreateCommand();
        // RESET ROLE first: if this pooled physical connection previously entered a bypass
        // scope (SET ROLE app_rls_bypass) and was reused before Npgsql's DISCARD ALL ran,
        // the elevated BYPASSRLS role would still be active and the GUC predicate would be
        // irrelevant. RESET ROLE returns the connection to the unprivileged app role so the
        // tenant_isolation policy binds. Parameter-free SQL combined with the GUC set so the
        // normal path still issues exactly one round-trip (CR-03).
        cmd.CommandText = "RESET ROLE; SET app.current_tenant_id = @tid";
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

        if (DapperRlsBypass.IsActive)
        {
            await using DbCommand bypassCmd = dbConnection.CreateCommand();
            // SET ROLE takes a SQL identifier, not a data parameter. _bypassRoleName is trusted
            // config (DapperRlsOptions.BypassRoleName), never user input — see options XML doc.
            bypassCmd.CommandText = $"SET ROLE {_bypassRoleName}";
            await bypassCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _log?.Warn("[DapperRls] BYPASS entered — SET ROLE {BypassRole} issued. Cross-tenant access active.", _bypassRoleName);
            return;
        }

        await using DbCommand cmd = dbConnection.CreateCommand();
        // RESET ROLE first: re-isolate a pooled connection that may still be running as the
        // bypass role from a prior scope (see sync Apply for the full rationale — CR-03).
        cmd.CommandText = "RESET ROLE; SET app.current_tenant_id = @tid";
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
