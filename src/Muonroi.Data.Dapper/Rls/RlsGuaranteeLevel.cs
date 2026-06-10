namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Indicates the strength of the Row-Level Security enforcement guarantee provided
/// by the configured database provider.
/// </summary>
/// <remarks>
/// The level is derived statically from the configured <see cref="DapperRlsProvider"/> —
/// it does not reflect runtime DDL-verification state. Consumers can resolve
/// <c>IRlsGuaranteeProvider</c> to read the level at runtime.
/// </remarks>
public enum RlsGuaranteeLevel
{
    /// <summary>
    /// Engine-level RLS — enforced natively by the database (PostgreSQL <c>CREATE POLICY</c>
    /// or SQL Server <c>CREATE SECURITY POLICY</c>). No row from another tenant can be read
    /// or written even if application-level filters are absent. Applies to
    /// <see cref="DapperRlsProvider.PostgreSql"/> and <see cref="DapperRlsProvider.MsSql"/>.
    /// </summary>
    Native,

    /// <summary>
    /// Emulated isolation — enforcement is via updatable views, <c>WITH CHECK OPTION</c>,
    /// and revoked base-table grants. Weaker than <see cref="Native"/>: anyone with
    /// base-table access can bypass the isolation. Reserved for the deferred
    /// <see cref="DapperRlsProvider.MySql"/> provider (v2+).
    /// </summary>
    Emulated
}
