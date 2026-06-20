using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Data.Dapper.Dapper;
using Muonroi.Data.Dapper.Rls.Setters;
using Muonroi.Logging.Abstractions;
using Muonroi.Tenancy.Abstractions;
using Npgsql;

namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Extension methods for wiring Dapper Row-Level Security into the dependency-injection container.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="AddMuonroiDapperRls"/> AFTER the provider-specific
/// <c>AddDapperForXxx</c> (e.g. <c>AddDapperForPostgreSQL</c>) so that the
/// <c>services.Replace</c> inside the enabled branch wins as the last registration
/// (last-wins semantics for open generics / service descriptors in .NET DI).
/// </para>
/// <para>
/// When <c>MultiTenantOptions.EnableRowLevelSecurity</c> is <see langword="false"/> (the default),
/// this method returns the <c>services</c> collection immediately — no setter is registered,
/// no <c>services.Replace</c> is called, and the vanilla <c>IDapper</c> descriptor from
/// <c>AddDapperForXxx</c> is left byte-for-byte untouched (CFG-01 zero-impact).
/// </para>
/// <para>
/// <b>Provider support (MSSQL/MySQL deferral)</b>:
/// Only <see cref="DapperRlsProvider.PostgreSql"/> is wired end-to-end. The <c>IDapper</c>
/// override is <see cref="TenantRlsDapper{TConn}"/> with <see cref="NpgsqlConnection"/> as
/// <c>TConn</c>. Selecting <see cref="DapperRlsProvider.MsSql"/> or
/// <see cref="DapperRlsProvider.MySql"/> throws <see cref="NotSupportedException"/> at
/// registration time (WR-03 fail-fast): wiring the Npgsql-typed override for those providers
/// would run their session SQL against an <see cref="NpgsqlConnection"/>, a silent
/// wrong-provider hazard. MSSQL <c>TConn</c> wiring (<c>SqlConnection</c>) arrives in Phase 3;
/// MySQL <c>TConn</c> wiring (<c>MySqlConnection</c>) in Phase 4.
/// </para>
/// </remarks>
public static class DapperRlsServiceCollectionExtensions
{
    /// <summary>
    /// Optionally replaces the registered <c>IDapper</c> with <see cref="TenantRlsDapper{TConn}"/>
    /// when <c>MultiTenantOptions.EnableRowLevelSecurity</c> is <see langword="true"/>.
    /// </summary>
    /// <param name="services">The service collection. Must not be <see langword="null"/>.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="DapperRlsOptions"/> (e.g. change the default
    /// provider from PostgreSQL to MSSQL or MySQL). Applied in addition to the bound configuration
    /// section <c>MultiTenantConfigs:DapperRls</c>.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance (for fluent chaining).</returns>
    public static IServiceCollection AddMuonroiDapperRls(
        this IServiceCollection services,
        Action<DapperRlsOptions>? configure = null)
    {
        MGuard.NotNull(services);

        // -----------------------------------------------------------------------
        // CFG-01 GATE — read registration-time options and return early if disabled.
        //
        // WR-02: read IConfiguration directly off the already-registered singleton
        // descriptor instead of calling services.BuildServiceProvider(). Building a
        // throwaway container at registration time eagerly instantiates/validates every
        // singleton registered so far, can trigger side effects, and produces a provider
        // whose singletons differ from the real container. IConfiguration is registered
        // as a singleton instance (AddSingleton<IConfiguration>(config)), so we can pull
        // it straight out of the descriptor with no container build.
        //
        // This is the ONLY place the gate decision is made. The services.Replace call
        // that follows is statically unreachable on the disabled path.
        // -----------------------------------------------------------------------
        IConfiguration? configuration = (IConfiguration?)services
            .LastOrDefault(sd => sd.ServiceType == typeof(IConfiguration))
            ?.ImplementationInstance;

        MultiTenantOptions multiTenantOpts = new();
        configuration?.GetSection(MultiTenantOptions.SectionName).Bind(multiTenantOpts);

        // CFG-01: return immediately without registering anything when RLS is disabled.
        if (!multiTenantOpts.EnableRowLevelSecurity)
        {
            return services;
        }

        // -----------------------------------------------------------------------
        // ENABLED BRANCH — resolve DapperRlsOptions and apply caller's configure action.
        // -----------------------------------------------------------------------
        DapperRlsOptions rlsOpts = new();
        configuration?.GetSection(DapperRlsOptions.SectionName).Bind(rlsOpts);
        configure?.Invoke(rlsOpts);

        // -----------------------------------------------------------------------
        // CFG-02: Register the provider-specific ITenantSessionContextSetter.
        //
        // Switch on the configured DapperRlsProvider. TryAddScoped ensures the setter
        // is only registered once even if AddMuonroiDapperRls is called multiple times.
        // -----------------------------------------------------------------------

        // -----------------------------------------------------------------------
        // CFG-02: Register the provider-specific ITenantSessionContextSetter and
        // perform the last-wins IDapper override with the correct TConn per provider.
        //
        // D-01: MSSQL is now wired end-to-end with TenantRlsDapper<SqlConnection> +
        // MsSqlTenantSessionContextSetter. The IDapper Replace lives INSIDE each
        // provider branch so the TConn is provider-correct (no wrong-provider hazard).
        // MySQL remains deferred (NotSupportedException).
        // -----------------------------------------------------------------------
        switch (rlsOpts.Provider)
        {
            case DapperRlsProvider.MsSql:
                services.TryAddScoped<ITenantSessionContextSetter>(sp =>
                    new MsSqlTenantSessionContextSetter(
                        log: sp.GetService<IMLog<MsSqlTenantSessionContextSetter>>()));
                services.Replace(ServiceDescriptor.Scoped<IDapper>(sp =>
                    new TenantRlsDapper<SqlConnection>(
                        sp,
                        connectionName: "default",
                        enableMasterSlave: false,
                        readOnly: false,
                        setter: sp.GetRequiredService<ITenantSessionContextSetter>(),
                        tenantContext: sp.GetRequiredService<ITenantContext>(),
                        strictMode: rlsOpts.StrictMode,
                        log: sp.GetService<IMLog<TenantRlsDapper<SqlConnection>>>())));
                break;

            case DapperRlsProvider.MySql:
                // WR-03: fail fast. MySQL emulated isolation is deferred to v2+.
                // Accepting the configuration explicitly rather than wiring a half-baked pipeline.
                throw new MInternalException(
                    $"Dapper RLS for provider '{rlsOpts.Provider}' is not yet available. " +
                    "MySQL emulated isolation is deferred to v2+. " +
                    "Use DapperRlsProvider.MsSql or DapperRlsProvider.PostgreSql.",
                    "NOT_SUPPORTED");

            case DapperRlsProvider.PostgreSql:
            default:
                services.TryAddScoped<ITenantSessionContextSetter>(sp =>
                    new PostgreSqlTenantSessionContextSetter(
                        bypassRoleName: rlsOpts.BypassRoleName,
                        log: sp.GetService<IMLog<PostgreSqlTenantSessionContextSetter>>()));
                services.Replace(ServiceDescriptor.Scoped<IDapper>(sp =>
                    new TenantRlsDapper<NpgsqlConnection>(
                        sp,
                        connectionName: "default",
                        enableMasterSlave: false,
                        readOnly: false,
                        setter: sp.GetRequiredService<ITenantSessionContextSetter>(),
                        tenantContext: sp.GetRequiredService<ITenantContext>(),
                        strictMode: rlsOpts.StrictMode,
                        log: sp.GetService<IMLog<TenantRlsDapper<NpgsqlConnection>>>())));
                break;
        }

        // HARD-04: register the static guarantee-level introspection service (D-09/D-10).
        // Capture rlsOpts.Provider at registration time — no IOptions, no BuildServiceProvider.
        services.TryAddSingleton<IRlsGuaranteeProvider>(new RlsGuaranteeProvider(rlsOpts.Provider));

        // HARD-01: register the startup verifier (D-02/D-03). The verifier is always registered
        // on the enabled branch; the VerifyRlsObjectsOnStartup opt-out is enforced inside
        // RlsStartupVerifier.StartingAsync (no DB round-trip when verify = false).
        // Captured reg-time values are passed via a factory delegate — no BuildServiceProvider.
        services.AddHostedService(sp => new RlsStartupVerifier(
            provider: rlsOpts.Provider,
            verify: rlsOpts.VerifyRlsObjectsOnStartup,
            connStrings: sp.GetRequiredService<IConnectionStringProvider>(),
            log: sp.GetService<IMLog<RlsStartupVerifier>>()));

        return services;
    }
}
