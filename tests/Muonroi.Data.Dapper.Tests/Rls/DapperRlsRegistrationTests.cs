using Dapper.Extensions;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Data.Dapper.Rls;
using Muonroi.Data.Dapper.Rls.Setters;
using Muonroi.Tenancy.Abstractions;
using NSubstitute;
using Npgsql;
using Xunit;

namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Tests for <see cref="DapperRlsServiceCollectionExtensions.AddMuonroiDapperRls"/> verifying:
/// <list type="bullet">
///   <item>CFG-01 zero-impact: disabled path returns early, leaving the vanilla IDapper descriptor byte-for-byte untouched.</item>
///   <item>CFG-02 provider registry: enabled path registers the matching setter for each provider.</item>
///   <item>Enabled path replaces IDapper with <see cref="TenantRlsDapper{TConn}"/> (Success Criterion 2).</item>
///   <item>Enabled MsSql/MySql Phase-1 deferral is observable (setter resolves; only PG TConn override is wired).</item>
/// </list>
/// </summary>
public sealed class DapperRlsRegistrationTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal <see cref="IServiceCollection"/> with in-memory config for
    /// <c>MultiTenantConfigs:EnableRowLevelSecurity</c> and <c>MultiTenantConfigs:DapperRls:Provider</c>,
    /// registers a baseline scoped <see cref="IDapper"/> (simulating what AddDapperForXxx does),
    /// and registers supporting services required by <see cref="TenantRlsDapper{TConn}"/> when enabled.
    /// </summary>
    private static IServiceCollection BuildBaseline(bool enableRls, string provider = "PostgreSql")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenantConfigs:EnableRowLevelSecurity"] = enableRls.ToString(),
                ["MultiTenantConfigs:DapperRls:Provider"] = provider
            })
            .Build();

        var sc = new ServiceCollection();
        sc.AddSingleton<IConfiguration>(config);
        sc.AddLogging();

        // Simulate what AddDapperForPostgreSQL would register — a scoped IDapper.
        // Use NSubstitute so the baseline type is definitively NOT TenantRlsDapper<>,
        // allowing the disabled-path assertion to hold regardless of test setup.
        // Use a factory delegate so we can capture the exact descriptor instance.
        sc.AddScoped<IDapper>(_ => Substitute.For<IDapper>());

        // IConnectionStringProvider is required by TenantRlsDapper (via BaseDapper) on the enabled path.
        sc.AddScoped<IConnectionStringProvider, TestConnectionStringProvider>();

        // ITenantContext required by TenantRlsDapper when enabled
        sc.AddScoped<ITenantContext>(_ => new SpyITenantContext("test-tenant"));

        return sc;
    }

    // -------------------------------------------------------------------------
    // CFG-01 / T-03-02: Disabled (default) — zero-impact proof
    // -------------------------------------------------------------------------

    [Fact]
    public void AddMuonroiDapperRls_WhenDisabled_DoesNotAlterIDapperDescriptor()
    {
        // Arrange
        IServiceCollection services = BuildBaseline(enableRls: false);
        ServiceDescriptor baselineDescriptor = services.Single(sd => sd.ServiceType == typeof(IDapper));

        // Act — call AddMuonroiDapperRls with RLS disabled (the default)
        services.AddMuonroiDapperRls();

        // Assert (a) — the IDapper descriptor is the SAME instance (services.Replace was not called)
        ServiceDescriptor afterDescriptor = services.Single(sd => sd.ServiceType == typeof(IDapper));
        afterDescriptor.Should().BeSameAs(baselineDescriptor,
            "AddMuonroiDapperRls must not call services.Replace when EnableRowLevelSecurity=false (CFG-01 zero-impact)");
    }

    [Fact]
    public void AddMuonroiDapperRls_WhenDisabled_IDapperDescriptorCountUnchanged()
    {
        // Arrange
        IServiceCollection services = BuildBaseline(enableRls: false);
        int baselineCount = services.Count(sd => sd.ServiceType == typeof(IDapper));

        // Act
        services.AddMuonroiDapperRls();

        // Assert (b) — count of IDapper descriptors is the same as the baseline
        int afterCount = services.Count(sd => sd.ServiceType == typeof(IDapper));
        afterCount.Should().Be(baselineCount,
            "no new IDapper descriptor must be added and none removed when disabled (CFG-01)");
    }

    [Fact]
    public void AddMuonroiDapperRls_WhenDisabled_ResolvedIDapperIsNotTenantRlsDapper()
    {
        // Arrange
        IServiceCollection services = BuildBaseline(enableRls: false);
        services.AddMuonroiDapperRls();

        // Act — build and resolve
        using ServiceProvider sp = services.BuildServiceProvider();
        IDapper resolved = sp.GetRequiredService<IDapper>();

        // Assert (c) — resolved IDapper is NOT a TenantRlsDapper<>
        resolved.Should().NotBeAssignableTo(typeof(TenantRlsDapper<NpgsqlConnection>),
            "when disabled the vanilla IDapper must be resolved, not TenantRlsDapper (CFG-01)");
    }

    [Fact]
    public void AddMuonroiDapperRls_WhenDisabled_ITenantSessionContextSetterNotRegistered()
    {
        // Arrange
        IServiceCollection services = BuildBaseline(enableRls: false);
        services.AddMuonroiDapperRls();

        // Act
        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert — no setter registered on the disabled path (early return before setter registration)
        ITenantSessionContextSetter? setter = sp.GetService<ITenantSessionContextSetter>();
        setter.Should().BeNull(
            "the setter must not be registered when AddMuonroiDapperRls returns early (CFG-01)");
    }

    // -------------------------------------------------------------------------
    // Success Criterion 2: Enabled + PostgreSql — resolved IDapper is TenantRlsDapper<NpgsqlConnection>
    // -------------------------------------------------------------------------

    [Fact]
    public void AddMuonroiDapperRls_WhenEnabledWithPostgreSql_ResolvedIDapperIsTenantRlsDapper()
    {
        // Arrange
        IServiceCollection services = BuildBaseline(enableRls: true, provider: "PostgreSql");
        services.AddMuonroiDapperRls();

        // Act
        using ServiceProvider sp = services.BuildServiceProvider();
        IDapper resolved = sp.GetRequiredService<IDapper>();

        // Assert
        resolved.Should().BeAssignableTo<TenantRlsDapper<NpgsqlConnection>>(
            "enabled + PostgreSql must resolve TenantRlsDapper<NpgsqlConnection> as IDapper (Success Criterion 2)");
    }

    // -------------------------------------------------------------------------
    // CFG-02 / Success Criterion 4: Provider registry — resolved setter type matches each provider
    // -------------------------------------------------------------------------

    [Fact]
    public void AddMuonroiDapperRls_WhenEnabledWithPostgreSql_SetterIsPostgreSqlSetter()
    {
        // Arrange
        IServiceCollection services = BuildBaseline(enableRls: true, provider: "PostgreSql");
        services.AddMuonroiDapperRls();

        // Act
        using ServiceProvider sp = services.BuildServiceProvider();
        ITenantSessionContextSetter setter = sp.GetRequiredService<ITenantSessionContextSetter>();

        // Assert
        setter.Should().BeOfType<PostgreSqlTenantSessionContextSetter>(
            "provider=PostgreSql must register PostgreSqlTenantSessionContextSetter (CFG-02)");
    }

    // -------------------------------------------------------------------------
    // D-01: MSSQL is now wired end-to-end — resolves TenantRlsDapper<SqlConnection>
    // + MsSqlTenantSessionContextSetter (no longer throws NotSupportedException).
    // -------------------------------------------------------------------------

    [Fact]
    public void AddMuonroiDapperRls_WhenEnabledWithMsSql_ResolvedIDapperIsTenantRlsDapperSqlConnection()
    {
        // Arrange
        IServiceCollection services = BuildBaseline(enableRls: true, provider: "MsSql");
        services.AddMuonroiDapperRls();

        // Act
        using ServiceProvider sp = services.BuildServiceProvider();
        IDapper resolved = sp.GetRequiredService<IDapper>();

        // Assert
        resolved.Should().BeAssignableTo<TenantRlsDapper<SqlConnection>>(
            "enabled + MsSql must resolve TenantRlsDapper<SqlConnection> as IDapper (D-01)");
    }

    [Fact]
    public void AddMuonroiDapperRls_WhenEnabledWithMsSql_SetterIsMsSqlSetter()
    {
        // Arrange
        IServiceCollection services = BuildBaseline(enableRls: true, provider: "MsSql");
        services.AddMuonroiDapperRls();

        // Act
        using ServiceProvider sp = services.BuildServiceProvider();
        ITenantSessionContextSetter setter = sp.GetRequiredService<ITenantSessionContextSetter>();

        // Assert
        setter.Should().BeOfType<MsSqlTenantSessionContextSetter>(
            "provider=MsSql must register MsSqlTenantSessionContextSetter (D-01 / CFG-02)");
    }

    // -------------------------------------------------------------------------
    // WR-03 fail-fast: MySQL is deferred to v2+.
    // -------------------------------------------------------------------------

    [Fact]
    public void AddMuonroiDapperRls_WhenEnabledWithMySql_ThrowsNotSupported()
    {
        // Arrange
        IServiceCollection services = BuildBaseline(enableRls: true, provider: "MySql");

        // Act
        Action act = () => services.AddMuonroiDapperRls();

        // Assert
        act.Should().Throw<MInternalException>(
            "WR-03: MySql is not wired end-to-end (arrives in Phase 4) and must fail fast at registration");
    }
}
