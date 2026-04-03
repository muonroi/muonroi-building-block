using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Core;
using System.Data.Common;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// EF Core connection interceptor that sets the PostgreSQL <c>app.current_tenant_id</c> session
/// variable on every connection open when <see cref="MultiTenantOptions.EnableRowLevelSecurity"/> is true.
///
/// This enables PostgreSQL Row-Level Security (RLS) policies to filter rows at the database engine level,
/// providing defense-in-depth isolation independent of EF query filters.
/// </summary>
public sealed class TenantRlsConnectionInterceptor : DbConnectionInterceptor
{
    private readonly IOptions<MultiTenantOptions> _options;

    /// <summary>
    /// Initializes a new instance of <see cref="TenantRlsConnectionInterceptor"/>.
    /// </summary>
    /// <param name="options">Multi-tenant configuration options.</param>
    public TenantRlsConnectionInterceptor(IOptions<MultiTenantOptions> options)
    {
        _options = MGuard.NotNull(options);
    }

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
        cmd.CommandText = "SET app.current_tenant_id = @tid";
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
        cmd.CommandText = "SET app.current_tenant_id = @tid";
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@tid";
        param.Value = TenantContext.CurrentTenantId ?? string.Empty;
        cmd.Parameters.Add(param);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
