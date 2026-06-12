using System.Threading;

namespace Muonroi.Data.Dapper.Rls.Bypass;

/// <summary>
/// Ambient, opt-in cross-tenant bypass for Dapper RLS. Enter a scope to make the PostgreSQL
/// session-context setter issue <c>SET ROLE &lt;bypassRoleName&gt;</c> (engine-level BYPASSRLS)
/// on the next connection open instead of <c>SET app.current_tenant_id</c>.
/// </summary>
/// <remarks>
/// <para>
/// The scope is backed by <see cref="AsyncLocal{T}"/> so it flows correctly through
/// <c>async</c>/<c>await</c> continuations. Bypass is never the default — it is only active
/// inside a live <see cref="Enter"/> scope, and every bypassed connection open is audit-logged
/// by the setter (D-06).
/// </para>
/// <para>
/// Usage:
/// <code>
/// using (DapperRlsBypass.Enter())
/// {
///     // Dapper queries here run cross-tenant (SET ROLE app_rls_bypass).
/// }
/// </code>
/// </para>
/// </remarks>
public static class DapperRlsBypass
{
    private static readonly AsyncLocal<bool> _bypassActive = new();

    /// <summary>
    /// Enters a cross-tenant bypass scope. Sets the ambient bypass flag and returns a disposable
    /// scope that clears the flag on disposal.
    /// </summary>
    /// <returns>An <see cref="IBypassScope"/>; dispose it (preferably via <c>using</c>) to exit bypass.</returns>
    public static IBypassScope Enter()
    {
        _bypassActive.Value = true;
        return new BypassScopeImpl();
    }

    /// <summary>
    /// Gets a value indicating whether a cross-tenant bypass scope is currently active on the
    /// calling async context.
    /// </summary>
    public static bool IsActive => _bypassActive.Value;

    private sealed class BypassScopeImpl : IBypassScope
    {
        public void Dispose() => _bypassActive.Value = false;
    }
}
