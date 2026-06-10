namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Selects the database provider whose <see cref="ITenantSessionContextSetter"/>
/// implementation will be activated when <c>MultiTenantOptions.EnableRowLevelSecurity</c>
/// is <see langword="true"/>.
/// </summary>
public enum DapperRlsProvider
{
    /// <summary>
    /// PostgreSQL — issues <c>SET app.current_tenant_id = @tid</c> (session scope, same GUC
    /// as the EF Core <c>TenantRlsConnectionInterceptor</c> so a single set of RLS policies
    /// serves both code paths).
    /// </summary>
    PostgreSql,

    /// <summary>
    /// Microsoft SQL Server — issues
    /// <c>EXEC sp_set_session_context @key=N'TenantId', @value=@tid, @read_only=1</c>
    /// (connection-scoped, native RLS via <c>CREATE SECURITY POLICY</c> + inline TVF).
    /// Requires SQL Server 2016+ / Azure SQL.
    /// </summary>
    MsSql,

    /// <summary>
    /// MySQL — issues <c>SET @app_current_tenant_id = @tid</c> (session user-variable).
    /// No native RLS; enforcement is via updatable views + <c>WITH CHECK OPTION</c> + revoked
    /// base-table grants. Isolation guarantee is weaker than PostgreSQL or MSSQL.
    /// </summary>
    MySql
}

/// <summary>
/// Configuration options for Dapper Row-Level Security provider selection.
/// Bind via <c>MultiTenantConfigs:DapperRls</c> in <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The on/off gate is NOT here — reuse <c>MultiTenantOptions.EnableRowLevelSecurity</c>.
/// This class contains only the provider-selection concern (CFG-02).
/// </remarks>
public sealed class DapperRlsOptions
{
    /// <summary>
    /// Configuration section name nested under <c>MultiTenantConfigs</c>.
    /// </summary>
    public const string SectionName = "MultiTenantConfigs:DapperRls";

    /// <summary>
    /// The database provider whose session-context setter will be registered.
    /// Defaults to <see cref="DapperRlsProvider.PostgreSql"/> — the most common
    /// provider in the Muonroi stack.
    /// </summary>
    public DapperRlsProvider Provider { get; set; } = DapperRlsProvider.PostgreSql;
}
