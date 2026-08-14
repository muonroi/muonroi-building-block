namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// EF Core connection interceptor that sets the PostgreSQL <c>app.current_tenant_id</c> session
/// variable on every connection open when <see cref="MultiTenantOptions.EnableRowLevelSecurity"/> is true.
///
/// This enables PostgreSQL Row-Level Security (RLS) policies to filter rows at the database engine level,
/// providing defense-in-depth isolation independent of EF query filters.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="TenantRlsConnectionInterceptor"/>.
/// </remarks>
/// <param name="options">Multi-tenant configuration options.</param>
public sealed class TenantRlsConnectionInterceptor(IOptions<MultiTenantOptions> options) : DbConnectionInterceptor
{
    private readonly IOptions<MultiTenantOptions> _options = MGuard.NotNull(options);

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantIdOnConnection(connection);
        base.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetTenantIdOnConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes <c>SET app.current_tenant_id = @tid</c> on <paramref name="connection"/>
    /// when RLS is enabled. Uses parameterized ADO.NET command to prevent SQL injection.
    /// </summary>
    internal void SetTenantIdOnConnection(DbConnection connection)
    {
        if (!_options.Value.EnableRowLevelSecurity)
        {
            return;
        }

        using DbCommand cmd = connection.CreateCommand();
        // PostgreSQL's SET command cannot take a bind parameter (SET x = $1 -> 42601 syntax error).
        // set_config(setting, value, is_local=false) is the parameterizable, session-scoped equivalent
        // and reads back via current_setting('app.current_tenant_id', true) in the RLS policies.
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tid, false)";
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@tid";
        param.Value = TenantContext.CurrentTenantId ?? string.Empty;
        cmd.Parameters.Add(param);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Async variant of <see cref="SetTenantIdOnConnection"/>.
    /// </summary>
    internal async Task SetTenantIdOnConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!_options.Value.EnableRowLevelSecurity)
        {
            return;
        }

        using DbCommand cmd = connection.CreateCommand();
        // PostgreSQL's SET command cannot take a bind parameter (SET x = $1 -> 42601 syntax error).
        // set_config(setting, value, is_local=false) is the parameterizable, session-scoped equivalent
        // and reads back via current_setting('app.current_tenant_id', true) in the RLS policies.
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tid, false)";
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@tid";
        param.Value = TenantContext.CurrentTenantId ?? string.Empty;
        cmd.Parameters.Add(param);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
