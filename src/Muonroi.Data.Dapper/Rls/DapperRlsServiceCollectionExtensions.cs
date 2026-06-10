using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Guards;
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
/// this method returns <paramref name="services"/> immediately — no setter is registered,
/// no <c>services.Replace</c> is called, and the vanilla <c>IDapper</c> descriptor from
/// <c>AddDapperForXxx</c> is left byte-for-byte untouched (CFG-01 zero-impact).
/// </para>
/// <para>
/// <b>Phase 1 deferral note</b> (MSSQL/MySQL TConn wiring):
/// The <c>IDapper</c> override is wired with <see cref="NpgsqlConnection"/> as <c>TConn</c>
/// for all providers in Phase 1. MSSQL <c>TConn</c> wiring (<c>SqlConnection</c>) is
/// deferred to Phase 3; MySQL <c>TConn</c> wiring (<c>MySqlConnection</c>) is deferred to
/// Phase 4. The matching provider <see cref="ITenantSessionContextSetter"/> IS registered
/// correctly for MSSQL/MySQL in Phase 1 — only the <c>TConn</c> type parameter of the
/// <c>IDapper</c> override is temporarily shared with PostgreSQL. This deferral is
/// observable and documented; no reader should mistake MSSQL/MySQL for end-to-end-wired
/// after reading this comment.
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
        // We build a transient service provider scoped only to this registration call
        // so we can read IConfiguration and resolve MultiTenantOptions without
        // side-effects on the caller's container.
        //
        // This is the ONLY place the gate decision is made. The services.Replace call
        // that follows is statically unreachable on the disabled path.
        // -----------------------------------------------------------------------
        using ServiceProvider tempSp = services.BuildServiceProvider();

        IConfiguration? configuration = tempSp.GetService<IConfiguration>();

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

        // MSSQL/MySQL TConn wiring deferred to Phases 3/4 (see class-level remarks).
        switch (rlsOpts.Provider)
        {
            case DapperRlsProvider.MsSql:
                services.TryAddScoped<ITenantSessionContextSetter, MsSqlTenantSessionContextSetter>();
                break;

            case DapperRlsProvider.MySql:
                services.TryAddScoped<ITenantSessionContextSetter, MySqlTenantSessionContextSetter>();
                break;

            case DapperRlsProvider.PostgreSql:
            default:
                services.TryAddScoped<ITenantSessionContextSetter>(sp =>
                    new PostgreSqlTenantSessionContextSetter(
                        bypassRoleName: rlsOpts.BypassRoleName,
                        log: sp.GetService<IMLog<PostgreSqlTenantSessionContextSetter>>()));
                break;
        }

        // -----------------------------------------------------------------------
        // Last-wins IDapper override: replace the vanilla IDapper from AddDapperForXxx
        // with TenantRlsDapper<NpgsqlConnection> (Phase 1 TConn).
        //
        // Phase 1 wires NpgsqlConnection for all providers. MSSQL/MySQL TConn wiring
        // deferred to Phases 3/4 as documented above.
        // -----------------------------------------------------------------------
        services.Replace(ServiceDescriptor.Scoped<IDapper>(sp =>
            new TenantRlsDapper<NpgsqlConnection>(
                sp,
                connectionName: "default",
                enableMasterSlave: false,
                readOnly: false,
                setter: sp.GetRequiredService<ITenantSessionContextSetter>(),
                tenantContext: sp.GetRequiredService<ITenantContext>(),
                log: sp.GetService<IMLog<TenantRlsDapper<NpgsqlConnection>>>())));

        return services;
    }
}
