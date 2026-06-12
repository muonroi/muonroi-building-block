namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Static per-provider mapping from <see cref="DapperRlsProvider"/> to
/// <see cref="RlsGuaranteeLevel"/>. Captured at DI registration time (D-10).
/// </summary>
internal sealed class RlsGuaranteeProvider : IRlsGuaranteeProvider
{
    /// <summary>
    /// Initializes a new instance of <see cref="RlsGuaranteeProvider"/> and computes
    /// the guarantee level for the specified provider.
    /// </summary>
    /// <param name="provider">The configured Dapper RLS provider.</param>
    public RlsGuaranteeProvider(DapperRlsProvider provider)
    {
        GuaranteeLevel = provider switch
        {
            DapperRlsProvider.PostgreSql => RlsGuaranteeLevel.Native,
            DapperRlsProvider.MsSql      => RlsGuaranteeLevel.Native,
            DapperRlsProvider.MySql      => RlsGuaranteeLevel.Emulated,
            _                            => RlsGuaranteeLevel.Native
        };
    }

    /// <inheritdoc />
    public RlsGuaranteeLevel GuaranteeLevel { get; }
}
