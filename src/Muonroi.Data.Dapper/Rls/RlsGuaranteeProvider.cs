namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Static per-provider mapping from <see cref="DapperRlsProvider"/> to
/// <see cref="RlsGuaranteeLevel"/>. Captured at DI registration time (D-10).
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="RlsGuaranteeProvider"/> and computes
/// the guarantee level for the specified provider.
/// </remarks>
/// <param name="provider">The configured Dapper RLS provider.</param>
internal sealed class RlsGuaranteeProvider(DapperRlsProvider provider) : IRlsGuaranteeProvider
{

    /// <inheritdoc />
    public RlsGuaranteeLevel GuaranteeLevel { get; } = provider switch
    {
        DapperRlsProvider.PostgreSql => RlsGuaranteeLevel.Native,
        DapperRlsProvider.MsSql => RlsGuaranteeLevel.Native,
        DapperRlsProvider.MySql => RlsGuaranteeLevel.Emulated,
        _ => RlsGuaranteeLevel.Native
    };
}
