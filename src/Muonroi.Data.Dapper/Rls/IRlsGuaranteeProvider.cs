namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Provides runtime introspection of the Row-Level Security enforcement strength for the
/// configured database provider (HARD-04, D-09).
/// </summary>
/// <remarks>
/// <para>
/// The guarantee level is derived statically from the configured <see cref="DapperRlsProvider"/>
/// at registration time — it does NOT reflect live DDL-verification state (D-10).
/// <c>Native</c> means engine-level enforcement is configured; it does not mean the required
/// DDL objects have been verified present (HARD-01 covers that).
/// </para>
/// <para>
/// This interface is intentionally separate from <c>IDapper</c> so it can be resolved
/// independently by operators/consumers without coupling to the data-access layer (D-09).
/// </para>
/// <para>
/// Registered as a singleton on the RLS-enabled branch by
/// <see cref="DapperRlsServiceCollectionExtensions.AddMuonroiDapperRls"/>.
/// Not registered on the disabled (default) path — resolve via <c>GetService</c> to handle
/// both cases gracefully.
/// </para>
/// </remarks>
public interface IRlsGuaranteeProvider
{
    /// <summary>
    /// Gets the RLS enforcement strength for the active provider.
    /// </summary>
    RlsGuaranteeLevel GuaranteeLevel { get; }
}
