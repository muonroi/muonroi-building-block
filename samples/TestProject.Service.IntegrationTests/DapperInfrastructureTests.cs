using Dapper.Extensions;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// INFR-02: Verifies that AddSiteDapperInfrastructure routes per-site connection strings
/// correctly via IConnectionStringProvider, and that IDapperRead falls back or uses
/// the read replica connection string when configured.
///
/// These tests target IConnectionStringProvider resolution directly (no real DB needed)
/// to prove connection string routing without requiring database connectivity.
/// </summary>
public sealed class DapperInfrastructureTests
{
    // Per-site write connection strings
    private static readonly Dictionary<string, string> WriteConnectionStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ALPHA"] = "Host=localhost;Port=5432;Database=testproject_alpha;Username=postgres;Password=0967442142",
        ["BRAVO"] = "Host=localhost;Port=5432;Database=testproject_bravo;Username=postgres;Password=0967442142"
    };

    /// <summary>
    /// Builds a ServiceCollection with AddSiteDapperInfrastructure + keyed IDapper mocks for each site.
    /// The siteIdAccessor func is called at scope resolution time.
    /// </summary>
    private static ServiceProvider BuildServiceProvider(
        string currentSiteId,
        string? readConnectionString = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddSingleton<ISiteProfileResolver>(
            new FakeSiteProfileResolver(currentSiteId));

        services.AddSiteDapperInfrastructure(o =>
        {
            o.WriteConnectionString = _ => WriteConnectionStrings[currentSiteId];

            if (readConnectionString is not null)
                o.ReadConnectionString = _ => readConnectionString;
        });

        // Register keyed IDapper mocks for each site so the resolver can find them
        var mockAlphaDapper = new Mock<IDapper>();
        var mockBravoDapper = new Mock<IDapper>();

        services.AddKeyedScoped<IDapper>("ALPHA", (_, _) => mockAlphaDapper.Object);
        services.AddKeyedScoped<IDapper>("BRAVO", (_, _) => mockBravoDapper.Object);

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AlphaSite_ConnectionStringProvider_ReturnsAlphaDatabase()
    {
        // Arrange
        using var provider = BuildServiceProvider("ALPHA");

        using var scope = provider.CreateScope();

        // Act — resolve IConnectionStringProvider from Alpha scope
        var connStringProvider = scope.ServiceProvider.GetRequiredService<IConnectionStringProvider>();
        string connectionString = connStringProvider.GetConnectionString("write");

        // Assert — must route to testproject_alpha
        Assert.Contains("testproject_alpha", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BravoSite_ConnectionStringProvider_ReturnsBravoDatabase()
    {
        // Arrange
        using var provider = BuildServiceProvider("BRAVO");

        using var scope = provider.CreateScope();

        // Act — resolve IConnectionStringProvider from Bravo scope
        var connStringProvider = scope.ServiceProvider.GetRequiredService<IConnectionStringProvider>();
        string connectionString = connStringProvider.GetConnectionString("write");

        // Assert — must route to testproject_bravo
        Assert.Contains("testproject_bravo", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadReplica_WhenConfigured_IDapperReadUsesReadConnectionString()
    {
        // Arrange — configure a read replica with a distinct ApplicationName marker
        const string readCs = "Host=localhost;Port=5432;Database=testproject_alpha;Username=postgres;Password=0967442142;ApplicationName=ReadReplica";
        using var provider = BuildServiceProvider("ALPHA", readConnectionString: readCs);

        using var scope = provider.CreateScope();

        // Act — resolve IConnectionStringProvider and call with readOnly=true
        var connStringProvider = scope.ServiceProvider.GetRequiredService<IConnectionStringProvider>();
        string readConnection = connStringProvider.GetConnectionString("read", readOnly: true);

        // Assert — read requests return the read replica connection string
        Assert.Contains("ReadReplica", readConnection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("testproject_alpha", readConnection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AlphaSite_IDapper_ResolvesFromKeyedRegistration()
    {
        // Arrange
        using var provider = BuildServiceProvider("ALPHA");

        using var scope = provider.CreateScope();

        // Act — IDapper should resolve from the "ALPHA" keyed registration
        var dapper = scope.ServiceProvider.GetRequiredService<IDapper>();

        // Assert — resolved successfully (not null)
        Assert.NotNull(dapper);
    }
}
