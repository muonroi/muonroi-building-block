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
    /// <c>EXEC sp_set_session_context @key=N'TenantId', @value=@tid</c>
    /// (connection-scoped, native RLS via <c>CREATE SECURITY POLICY</c> + inline TVF).
    /// <c>@read_only=1</c> is intentionally NOT used: it is incompatible with the
    /// set-per-open model (re-setting a read-only key on a reused connection throws).
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

    /// <summary>
    /// The PostgreSQL role name granted the <c>BYPASSRLS</c> attribute, entered via
    /// <c>SET ROLE</c> when <c>DapperRlsBypass.IsActive</c> is <see langword="true"/>.
    /// Defaults to <c>app_rls_bypass</c> (the role provisioned by migration 0002).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The role name is used directly in the <c>SET ROLE</c> command text — it is a SQL
    /// identifier, NOT a data parameter (PostgreSQL does not accept a bound parameter for an
    /// identifier). It MUST therefore come from trusted configuration, never from user input.
    /// </para>
    /// <para>
    /// Validate that this value matches the role provisioned in migration 0002. Configurable
    /// via <c>MultiTenantConfigs:DapperRls:BypassRoleName</c> or the options delegate.
    /// </para>
    /// </remarks>
    public string BypassRoleName { get; set; } = "app_rls_bypass";

    /// <summary>
    /// When <see langword="true"/>, <see cref="TenantRlsDapper{TConn}"/> throws a
    /// <c>MissingTenantContextException</c> at query time if the ambient tenant id is
    /// <see langword="null"/> or whitespace and no bypass scope is active.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/> — behavior is byte-identical to v1.0 when off
    /// (ROADMAP criterion #3). Enable to make misconfigured deployments fail loud instead
    /// of silently filtering all rows. The sanctioned <c>DapperRlsBypass</c> scope always
    /// suppresses the throw regardless of this setting.
    /// </remarks>
    public bool StrictMode { get; set; } = false;

    /// <summary>
    /// When <see langword="true"/> (the default), <c>RlsStartupVerifier</c> checks that the
    /// required RLS database objects exist for the configured provider during host startup
    /// and throws if any are missing (HARD-01).
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> — the primary purpose is catching
    /// enabled-but-DDL-not-applied deployments at startup before any query runs.
    /// Set to <see langword="false"/> only as an escape hatch for edge cases such as a
    /// database that is unreachable at boot or DDL applied by a later migration step.
    /// </remarks>
    public bool VerifyRlsObjectsOnStartup { get; set; } = true;
}
