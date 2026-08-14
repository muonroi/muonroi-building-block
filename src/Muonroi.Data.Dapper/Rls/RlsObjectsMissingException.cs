namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Exception thrown by <see cref="RlsStartupVerifier"/> when host startup is aborted because
/// required RLS DDL objects are absent or disabled for the configured provider.
/// </summary>
/// <remarks>
/// <para>
/// Thrown from <c>IHostedLifecycleService.StartingAsync</c> to fail the host loudly before it
/// serves any traffic. The message names the provider and the missing objects and hints at the
/// corrective action (apply the migration DDL).
/// </para>
/// <para>
/// <b>PostgreSQL:</b> expects the <c>tenant_isolation</c> policy to exist on every table with a
/// <c>tenant_id</c> column AND <c>pg_class.relforcerowsecurity = true</c> on every such table
/// (created by <c>0001_enable_rls_postgres.sql</c>).
/// </para>
/// <para>
/// <b>MSSQL:</b> expects <c>dbo.fn_tenant_access</c> (inline TVF) AND an enabled
/// <c>&lt;table&gt;_TenantIsolation</c> SECURITY POLICY for every table with a <c>tenant_id</c>
/// column (created by <c>0001_enable_rls_sqlserver.sql</c> + <c>0002_sqlserver_tenant_rls.sql</c>).
/// </para>
/// <para>
/// Opt-out: set <see cref="DapperRlsOptions.VerifyRlsObjectsOnStartup"/> to
/// <see langword="false"/> to skip the check (e.g. when the DB is unreachable at boot or the
/// migration runs after startup).
/// </para>
/// </remarks>
public sealed class RlsObjectsMissingException : MInternalException
{
    /// <summary>
    /// Initializes a new instance of <see cref="RlsObjectsMissingException"/> with an actionable
    /// message that names the provider and the expected DDL objects.
    /// </summary>
    /// <param name="provider">The configured Dapper RLS provider.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    public RlsObjectsMissingException(
        DapperRlsProvider provider,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        : base(BuildMessage(provider), "RLS_OBJECTS_MISSING", callerMember, callerFile, callerLine)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RlsObjectsMissingException"/> with a custom
    /// message.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    public RlsObjectsMissingException(
        string message,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        : base(message, "RLS_OBJECTS_MISSING", callerMember, callerFile, callerLine)
    {
    }

    private static string BuildMessage(DapperRlsProvider provider) => provider switch
    {
        DapperRlsProvider.PostgreSql =>
            "RLS startup verification FAILED for provider PostgreSql. " +
            "Required DDL objects are missing or disabled. " +
            "Expected: policy 'tenant_isolation' on every table with a 'tenant_id' column " +
            "AND FORCE ROW LEVEL SECURITY enabled (pg_class.relforcerowsecurity = true) on every such table. " +
            "Apply migration '0001_enable_rls_postgres.sql' to create the required objects. " +
            "To skip this check, set DapperRlsOptions.VerifyRlsObjectsOnStartup = false.",

        DapperRlsProvider.MsSql =>
            "RLS startup verification FAILED for provider MsSql. " +
            "Required DDL objects are missing or disabled. " +
            "Expected: inline TVF 'dbo.fn_tenant_access' AND an enabled '<table>_TenantIsolation' " +
            "SECURITY POLICY for every table with a 'tenant_id' column. " +
            "Apply migrations '0001_enable_rls_sqlserver.sql' and '0002_sqlserver_tenant_rls.sql' " +
            "to create the required objects. " +
            "To skip this check, set DapperRlsOptions.VerifyRlsObjectsOnStartup = false.",

        _ =>
            $"RLS startup verification FAILED for provider '{provider}'. " +
            "Required DDL objects are missing or disabled. " +
            "Apply the provider-specific RLS migration DDL. " +
            "To skip this check, set DapperRlsOptions.VerifyRlsObjectsOnStartup = false."
    };
}
