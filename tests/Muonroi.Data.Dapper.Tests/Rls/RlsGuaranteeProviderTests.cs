namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Tests for <see cref="IRlsGuaranteeProvider"/> / <see cref="RlsGuaranteeProvider"/>:
/// static provider-to-level mapping and DI resolution (HARD-04).
/// </summary>
public sealed class RlsGuaranteeProviderTests
{
    // -------------------------------------------------------------------------
    // Direct-construction mapping (D-10)
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "RlsGuaranteeProvider maps provider to expected GuaranteeLevel")]
    [InlineData(DapperRlsProvider.PostgreSql, RlsGuaranteeLevel.Native)]
    [InlineData(DapperRlsProvider.MsSql, RlsGuaranteeLevel.Native)]
    [InlineData(DapperRlsProvider.MySql, RlsGuaranteeLevel.Emulated)]
    public void GuaranteeLevel_ReturnsExpectedLevelForProvider(DapperRlsProvider provider, RlsGuaranteeLevel expectedLevel)
    {
        // Act — direct construction (no DI required for unit mapping test)
        IRlsGuaranteeProvider sut = new RlsGuaranteeProvider(provider);

        // Assert
        sut.GuaranteeLevel.Should().Be(expectedLevel,
            because: $"provider {provider} must map to {expectedLevel} (D-10 static mapping)");
    }

    // -------------------------------------------------------------------------
    // DI resolution — enabled MsSql provider resolves IRlsGuaranteeProvider → Native
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Enabled + MsSql provider: IRlsGuaranteeProvider resolves from DI and reports Native")]
    public void AddMuonroiDapperRls_WhenEnabledMsSql_IRlsGuaranteeProviderResolvesNative()
    {
        // Arrange — mirror DapperRlsRegistrationTests.BuildBaseline for MsSql
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenantConfigs:EnableRowLevelSecurity"] = "true",
                ["MultiTenantConfigs:DapperRls:Provider"] = "MsSql"
            })
            .Build();

        var sc = new ServiceCollection();
        sc.AddSingleton<IConfiguration>(config);
        sc.AddLogging();
        sc.AddScoped<IDapper>(_ => Substitute.For<IDapper>());
        sc.AddScoped<IConnectionStringProvider, TestConnectionStringProvider>();
        sc.AddScoped<ITenantContext>(_ => new SpyITenantContext("test-tenant"));

        sc.AddMuonroiDapperRls();

        // Act
        using ServiceProvider sp = sc.BuildServiceProvider();
        IRlsGuaranteeProvider? provider = sp.GetService<IRlsGuaranteeProvider>();

        // Assert
        provider.Should().NotBeNull(
            because: "IRlsGuaranteeProvider must be registered on the enabled branch");
        provider!.GuaranteeLevel.Should().Be(RlsGuaranteeLevel.Native,
            because: "MsSql maps to Native guarantee level (D-10)");
    }

    [Fact(DisplayName = "Enabled + PostgreSql provider: IRlsGuaranteeProvider resolves from DI and reports Native")]
    public void AddMuonroiDapperRls_WhenEnabledPostgreSql_IRlsGuaranteeProviderResolvesNative()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenantConfigs:EnableRowLevelSecurity"] = "true",
                ["MultiTenantConfigs:DapperRls:Provider"] = "PostgreSql"
            })
            .Build();

        var sc = new ServiceCollection();
        sc.AddSingleton<IConfiguration>(config);
        sc.AddLogging();
        sc.AddScoped<IDapper>(_ => Substitute.For<IDapper>());
        sc.AddScoped<IConnectionStringProvider, TestConnectionStringProvider>();
        sc.AddScoped<ITenantContext>(_ => new SpyITenantContext("test-tenant"));

        sc.AddMuonroiDapperRls();

        // Act
        using ServiceProvider sp = sc.BuildServiceProvider();
        IRlsGuaranteeProvider? provider = sp.GetService<IRlsGuaranteeProvider>();

        // Assert
        provider.Should().NotBeNull(
            because: "IRlsGuaranteeProvider must be registered on the enabled branch");
        provider!.GuaranteeLevel.Should().Be(RlsGuaranteeLevel.Native,
            because: "PostgreSql maps to Native guarantee level (D-10)");
    }

    [Fact(DisplayName = "Disabled path: IRlsGuaranteeProvider is NOT registered")]
    public void AddMuonroiDapperRls_WhenDisabled_IRlsGuaranteeProviderNotRegistered()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenantConfigs:EnableRowLevelSecurity"] = "false"
            })
            .Build();

        var sc = new ServiceCollection();
        sc.AddSingleton<IConfiguration>(config);
        sc.AddLogging();
        sc.AddScoped<IDapper>(_ => Substitute.For<IDapper>());

        sc.AddMuonroiDapperRls();

        // Act
        using ServiceProvider sp = sc.BuildServiceProvider();
        IRlsGuaranteeProvider? provider = sp.GetService<IRlsGuaranteeProvider>();

        // Assert
        provider.Should().BeNull(
            because: "IRlsGuaranteeProvider must NOT be registered on the disabled path (CFG-01 zero-impact)");
    }
}
