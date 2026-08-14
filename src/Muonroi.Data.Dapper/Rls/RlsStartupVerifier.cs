namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Startup-time verifier (HARD-01) that fails host startup loud when the configured provider's
/// required RLS DDL objects are absent or disabled.
/// </summary>
/// <remarks>
/// <para>
/// Registered as an <see cref="IHostedLifecycleService"/> on the enabled RLS branch. The host
/// invokes <see cref="StartingAsync"/> before any <c>IHostedService.StartAsync</c>, so a throw
/// from <see cref="StartingAsync"/> aborts host startup before the application serves any
/// traffic (D-02 / A2).
/// </para>
/// <para>
/// The check opens a PLAIN <see cref="SqlConnection"/> or <see cref="NpgsqlConnection"/>
/// directly — it never routes through <c>IDapper</c>/<c>TenantRlsDapper</c> (D-05), so it
/// neither applies tenant session-context nor triggers strict-mode before RLS is validated.
/// </para>
/// <para>
/// Opt-out: set <see cref="DapperRlsOptions.VerifyRlsObjectsOnStartup"/> to
/// <see langword="false"/> to skip the DB round-trip entirely (D-03 escape hatch for
/// boot-ordering / migration-later scenarios).
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of <see cref="RlsStartupVerifier"/>.
/// All values are captured at DI registration time — no <c>IOptions</c>, no
/// <c>BuildServiceProvider</c>.
/// </remarks>
/// <param name="provider">The configured Dapper RLS provider (PG, MSSQL).</param>
/// <param name="verify">
/// Whether to run the catalog check (<see cref="DapperRlsOptions.VerifyRlsObjectsOnStartup"/>).
/// </param>
/// <param name="connStrings">
/// Connection-string provider. <c>GetConnectionString("default", false, false)</c> is called
/// to obtain the same connection string used by the <c>IDapper</c> registration.
/// </param>
/// <param name="log">Optional logger for the success / skip paths.</param>
internal sealed class RlsStartupVerifier(
    DapperRlsProvider provider,
    bool verify,
    IConnectionStringProvider connStrings,
    IMLog<RlsStartupVerifier>? log = null) : IHostedLifecycleService
{
    private readonly DapperRlsProvider _provider = provider;
    private readonly bool _verify = verify;
    private readonly IConnectionStringProvider _connStrings = connStrings;
    private readonly IMLog<RlsStartupVerifier>? _log = log;

    /// <inheritdoc />
    /// <summary>
    /// Runs the per-provider RLS DDL-presence catalog query on a plain provider connection.
    /// Throws <see cref="RlsObjectsMissingException"/> to abort host startup when objects are
    /// missing or disabled. Returns immediately (no DB round-trip) when
    /// <see cref="DapperRlsOptions.VerifyRlsObjectsOnStartup"/> is <see langword="false"/>.
    /// </summary>
    public async Task StartingAsync(CancellationToken ct)
    {
        // D-03 opt-out: skip check when disabled (boot-ordering / migration-later escape hatch).
        if (!_verify)
        {
            _log?.Info("[DapperRls] RLS startup verification skipped (VerifyRlsObjectsOnStartup = false).");
            return;
        }

        // Resolve the same connection string the IDapper registration uses — key "default" (D-03 / D-05).
        string cs = _connStrings.GetConnectionString("default", false, false);

        // Open a PLAIN provider DbConnection — never through IDapper/TenantRlsDapper (D-05).
        MGuard.State(_provider is DapperRlsProvider.MsSql or DapperRlsProvider.PostgreSql, $"RLS startup verification is not supported for provider '{_provider}'.", "NOT_SUPPORTED");
        
        DbConnection conn = _provider switch
        {
            DapperRlsProvider.MsSql => new SqlConnection(cs),
            DapperRlsProvider.PostgreSql => new NpgsqlConnection(cs),
            _ => null!
        };

        await using (conn)
        {
            await conn.OpenAsync(ct).ConfigureAwait(false);

            bool healthy = await RlsObjectPresenceQueries.IsHealthyAsync(_provider, conn, ct)
                .ConfigureAwait(false);

            if (!healthy)
            {
                throw new RlsObjectsMissingException(_provider);
            }
        }

        _log?.Info("[DapperRls] RLS startup verification passed for provider {Provider}.", _provider.ToString());
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken ct) => Task.CompletedTask;
}
