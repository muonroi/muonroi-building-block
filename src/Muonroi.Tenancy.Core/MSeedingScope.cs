namespace Muonroi.Tenancy.Core;

/// <summary>
/// Ambient scope that enables cross-tenant / cross-creator access for the lifetime of a
/// bootstrap or seeding operation, restoring the previous value on dispose.
/// </summary>
/// <remarks>
/// Seeding runs before any user is authenticated. The runtime security query filters
/// (tenant + creator, both fail-closed) would otherwise hide already-seeded rows from the
/// seeder's existence checks while unfiltered unique indexes (e.g. IX_MRoles_Name) still
/// reject a re-insert — making seeding crash on every boot after the first.
/// Wrapping seeding in this scope makes every query executed inside it bypass those filters,
/// so callers no longer have to remember a per-query <c>IgnoreQueryFilters()</c> (a fragile
/// pattern: one forgotten call reintroduces the crash). Because the flag is AsyncLocal it is
/// isolated to the seeding execution context and never leaks to request-handling code.
/// </remarks>
public sealed class MSeedingScope : IDisposable
{
    private readonly bool _previousAllowCrossTenant;
    private bool _disposed;

    /// <summary>
    /// Initializes the scope, enabling cross-tenant/cross-creator access.
    /// </summary>
    public MSeedingScope()
    {
        _previousAllowCrossTenant = TenantContext.AllowCrossTenantAccess;
        TenantContext.AllowCrossTenantAccess = true;
    }

    /// <summary>
    /// Restores the previous cross-tenant access flag.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        TenantContext.AllowCrossTenantAccess = _previousAllowCrossTenant;
        _disposed = true;
    }
}
