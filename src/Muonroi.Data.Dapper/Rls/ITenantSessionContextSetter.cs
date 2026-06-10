using System.Data;

namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Provider-agnostic contract for setting the current tenant's session context on a database connection.
/// Each supported engine (PostgreSQL, MSSQL, MySQL) supplies a concrete implementation.
/// </summary>
/// <remarks>
/// Implementations are invoked on every connection open (before any Dapper command executes).
/// The caller is responsible for gating on <c>EnableRowLevelSecurity</c> — this interface
/// is only ever called on the enabled path.
/// </remarks>
public interface ITenantSessionContextSetter
{
    /// <summary>
    /// Synchronously sets the tenant session context on the open <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">An already-open database connection.</param>
    /// <param name="tenantId">
    /// The current tenant identifier, or <see langword="null"/> / empty when no tenant context
    /// is available (the engine's RLS policy will then block all rows).
    /// </param>
    void Apply(IDbConnection connection, string? tenantId);

    /// <summary>
    /// Asynchronously sets the tenant session context on the open <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">An already-open database connection.</param>
    /// <param name="tenantId">
    /// The current tenant identifier, or <see langword="null"/> / empty when no tenant context
    /// is available (the engine's RLS policy will then block all rows).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task ApplyAsync(IDbConnection connection, string? tenantId, CancellationToken ct = default);
}
