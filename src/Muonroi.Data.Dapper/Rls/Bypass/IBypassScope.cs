namespace Muonroi.Data.Dapper.Rls.Bypass;

/// <summary>
/// Represents a scoped cross-tenant bypass for Dapper RLS, disposable.
/// </summary>
/// <remarks>
/// <para>
/// Always use in a synchronous <c>using</c> block that wraps the awaited operations which
/// must run with the bypass active. While the scope is undisposed,
/// <see cref="DapperRlsBypass.IsActive"/> returns <see langword="true"/> and the PostgreSQL
/// setter issues <c>SET ROLE &lt;bypassRoleName&gt;</c> on connection open instead of the
/// per-tenant GUC.
/// </para>
/// <para>
/// Fire-and-forget tasks started inside the scope inherit the bypass via
/// <see cref="System.Threading.AsyncLocal{T}"/> copy-on-capture semantics, but disposing the
/// scope does NOT flip those already-captured copies. Do not rely on disposal to cancel
/// bypass in detached child work — keep all bypassed operations inside the <c>using</c> block.
/// </para>
/// </remarks>
public interface IBypassScope : IDisposable
{
}
