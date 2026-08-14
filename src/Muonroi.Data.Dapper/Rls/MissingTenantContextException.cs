namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Exception thrown by <see cref="TenantRlsDapper{TConn}"/> when strict-mode is enabled,
/// the ambient tenant id is absent (null or whitespace), and no bypass scope is active.
/// </summary>
/// <remarks>
/// Strict-mode is configured via <see cref="DapperRlsOptions.StrictMode"/>. When on, a
/// missing tenant context causes a loud failure at the Dapper query choke point rather than
/// silently filtering all rows (the v1.0 default). The sanctioned <c>DapperRlsBypass</c>
/// scope always suppresses this exception regardless of strict-mode (D-07).
/// </remarks>
public sealed class MissingTenantContextException : MInternalException
{
    private const string DefaultMessage =
        "RLS strict-mode is enabled but no tenant context is present and no bypass scope is active. " +
        "Set the ambient ITenantContext.TenantId or wrap the call in DapperRlsBypass.Enter() if cross-tenant access is intentional.";

    /// <summary>
    /// Initializes a new instance of <see cref="MissingTenantContextException"/> with the
    /// default actionable message.
    /// </summary>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    public MissingTenantContextException(
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        : base(DefaultMessage, "RLS_MISSING_TENANT_CONTEXT", callerMember, callerFile, callerLine)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MissingTenantContextException"/> with a
    /// custom message.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    public MissingTenantContextException(
        string message,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        : base(message, "RLS_MISSING_TENANT_CONTEXT", callerMember, callerFile, callerLine)
    {
    }
}
