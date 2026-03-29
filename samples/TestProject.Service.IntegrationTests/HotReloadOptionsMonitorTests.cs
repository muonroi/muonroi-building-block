using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-25: Integration tests for IOptionsMonitor hot-reload via actual file-based configuration.
///
/// These tests close UAT Gap 3 (minor): the prior BreakResilienceTests.HotReloadConfigIsolation
/// test simulated config isolation via two separate ServiceProvider builds.
/// These tests use real file I/O — File.WriteAllText to appsettings.json — and verify that
/// IOptionsMonitor.OnChange callback fires for the modified section, and that the updated
/// value is reflected in IOptionsMonitor.CurrentValue after reload.
///
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// All tests create temp directories and clean up after themselves.
/// </summary>
public sealed class HotReloadOptionsMonitorTests : IDisposable
{
    // ---------------------------------------------------------------------------
    // POCOs — defined inline to avoid test project coupling to domain types
    // ---------------------------------------------------------------------------

    private sealed class BravoSiteOptions
    {
        public string ConnectionString { get; set; } = "";
        public int MaxConnections { get; set; } = 5;
    }

    private sealed class DefaultSiteOptions
    {
        public string ConnectionString { get; set; } = "";
        public int MaxConnections { get; set; } = 5;
    }

    // ---------------------------------------------------------------------------
    // Temp directory tracking for cleanup
    // ---------------------------------------------------------------------------

    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (string dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }

    // ---------------------------------------------------------------------------
    // Helper: create a temp directory and register it for cleanup
    // ---------------------------------------------------------------------------

    private string CreateTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hotreload_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    // ---------------------------------------------------------------------------
    // Helper: write appsettings.json content to the given path
    // ---------------------------------------------------------------------------

    private static void WriteSettings(string path, string bravoConnStr, int bravoMaxConn, string defaultConnStr)
    {
        string json = $$"""
            {
              "Sites": {
                "BRAVO": {
                  "ConnectionString": "{{bravoConnStr}}",
                  "MaxConnections": {{bravoMaxConn}}
                },
                "DEFAULT": {
                  "ConnectionString": "{{defaultConnStr}}",
                  "MaxConnections": 5
                }
              }
            }
            """;
        File.WriteAllText(path, json);
    }

    // ===========================================================================
    // Test 1: File change triggers IOptionsMonitor callback for BRAVO section,
    //         DEFAULT section value remains unchanged.
    // ===========================================================================

    [Fact]
    public async Task FileChange_TriggersOptionsMonitorCallback_BravoValueChanges_DefaultUnchanged()
    {
        // [TEST:BREAKPOINT] HotReloadOptionsMonitorTests - FileChange callback isolation
        string tempDir = CreateTempDir();
        string settingsPath = Path.Combine(tempDir, "appsettings.json");

        // Write initial settings
        WriteSettings(settingsPath,
            bravoConnStr: "Host=localhost;Database=bravo_db",
            bravoMaxConn: 10,
            defaultConnStr: "Host=localhost;Database=default_db");

        // Build IConfiguration with file-based reload enabled
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(tempDir)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Wire up DI with IOptionsMonitor bound to each section
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<BravoSiteOptions>(configuration.GetSection("Sites:BRAVO"));
        services.Configure<DefaultSiteOptions>(configuration.GetSection("Sites:DEFAULT"));

        using var provider = services.BuildServiceProvider();

        var bravoMonitor = provider.GetRequiredService<IOptionsMonitor<BravoSiteOptions>>();
        var defaultMonitor = provider.GetRequiredService<IOptionsMonitor<DefaultSiteOptions>>();

        // Capture initial values
        string initialBravoConn = bravoMonitor.CurrentValue.ConnectionString;
        string initialDefaultConn = defaultMonitor.CurrentValue.ConnectionString;

        Assert.Equal("Host=localhost;Database=bravo_db", initialBravoConn);
        Assert.Equal("Host=localhost;Database=default_db", initialDefaultConn);

        // Subscribe to change callbacks
        using var bravoFired = new ManualResetEventSlim(false);
        BravoSiteOptions? changedBravoValue = null;

        using var bravoReg = bravoMonitor.OnChange(newValue =>
        {
            changedBravoValue = newValue;
            bravoFired.Set();
        });

        // Modify ONLY the Sites:BRAVO:ConnectionString in the JSON file
        // Note: file-based config provider reloads the entire file but we can verify
        //       that the BRAVO value changed and the DEFAULT value stayed the same.
        // Small delay to ensure the file watcher is ready after subscription
        await Task.Delay(100);

        WriteSettings(settingsPath,
            bravoConnStr: "Host=prod-server;Database=bravo_prod_db",
            bravoMaxConn: 10,
            defaultConnStr: "Host=localhost;Database=default_db"); // DEFAULT unchanged

        // Wait up to 5 seconds for the BRAVO change callback to fire
        // (file watcher has ~250ms debounce on Windows)
        bool bravoCallbackFired = bravoFired.Wait(TimeSpan.FromSeconds(5));

        Assert.True(bravoCallbackFired,
            "Expected IOptionsMonitor<BravoSiteOptions>.OnChange to fire within 5 seconds after file write.");

        // Assert: BRAVO connection string was updated in the callback value
        Assert.NotNull(changedBravoValue);
        Assert.Equal("Host=prod-server;Database=bravo_prod_db", changedBravoValue.ConnectionString);

        // Assert: DEFAULT connection string remains unchanged after file reload
        // Note: file reload triggers callbacks for ALL sections bound to the file,
        //       but the VALUE should be unchanged for DEFAULT since we didn't change it.
        string defaultConnAfterReload = defaultMonitor.CurrentValue.ConnectionString;
        Assert.Equal("Host=localhost;Database=default_db", defaultConnAfterReload);
    }

    // ===========================================================================
    // Test 2: CurrentValue reflects the updated value after file write
    // ===========================================================================

    [Fact]
    public async Task FileChange_UpdatedValue_ReflectedInCurrentValue()
    {
        // [TEST:BREAKPOINT] HotReloadOptionsMonitorTests - CurrentValue reflects updated value
        string tempDir = CreateTempDir();
        string settingsPath = Path.Combine(tempDir, "appsettings.json");

        // Write initial settings with MaxConnections = 10
        WriteSettings(settingsPath,
            bravoConnStr: "Host=localhost;Database=bravo_db",
            bravoMaxConn: 10,
            defaultConnStr: "Host=localhost;Database=default_db");

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(tempDir)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<BravoSiteOptions>(configuration.GetSection("Sites:BRAVO"));

        using var provider = services.BuildServiceProvider();
        var bravoMonitor = provider.GetRequiredService<IOptionsMonitor<BravoSiteOptions>>();

        // Assert initial value
        Assert.Equal(10, bravoMonitor.CurrentValue.MaxConnections);

        // Subscribe to detect when reload completes
        using var reloadFired = new ManualResetEventSlim(false);
        using var reg = bravoMonitor.OnChange(_ => reloadFired.Set());

        // Allow file watcher to initialize
        await Task.Delay(100);

        // Overwrite appsettings.json with MaxConnections = 50
        WriteSettings(settingsPath,
            bravoConnStr: "Host=localhost;Database=bravo_db",
            bravoMaxConn: 50,
            defaultConnStr: "Host=localhost;Database=default_db");

        // Wait for the OnChange callback to fire (proves the file watcher fired)
        bool reloaded = reloadFired.Wait(TimeSpan.FromSeconds(5));

        Assert.True(reloaded,
            "Expected IOptionsMonitor<BravoSiteOptions>.OnChange to fire within 5 seconds after file write.");

        // Assert: CurrentValue now reflects the updated MaxConnections
        Assert.Equal(50, bravoMonitor.CurrentValue.MaxConnections);
    }

    // ===========================================================================
    // Test 3: Verify configuration section isolation — unrelated section is stable
    // ===========================================================================

    [Fact]
    public async Task FileChange_OnlyBravoSection_DefaultSectionValueStable()
    {
        // [TEST:BREAKPOINT] HotReloadOptionsMonitorTests - Section isolation
        string tempDir = CreateTempDir();
        string settingsPath = Path.Combine(tempDir, "appsettings.json");

        // Write initial settings
        WriteSettings(settingsPath,
            bravoConnStr: "Host=localhost;Database=bravo_initial",
            bravoMaxConn: 5,
            defaultConnStr: "Host=localhost;Database=default_stable");

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(tempDir)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<BravoSiteOptions>(configuration.GetSection("Sites:BRAVO"));
        services.Configure<DefaultSiteOptions>(configuration.GetSection("Sites:DEFAULT"));

        using var provider = services.BuildServiceProvider();
        var bravoMonitor = provider.GetRequiredService<IOptionsMonitor<BravoSiteOptions>>();
        var defaultMonitor = provider.GetRequiredService<IOptionsMonitor<DefaultSiteOptions>>();

        // Capture initial DEFAULT value
        string stableDefaultConn = defaultMonitor.CurrentValue.ConnectionString;
        Assert.Equal("Host=localhost;Database=default_stable", stableDefaultConn);

        using var bravoFired = new ManualResetEventSlim(false);
        using var bravoReg = bravoMonitor.OnChange(_ => bravoFired.Set());

        // Allow file watcher to initialize
        await Task.Delay(100);

        // Change ONLY BRAVO ConnectionString — DEFAULT remains identical
        WriteSettings(settingsPath,
            bravoConnStr: "Host=new-server;Database=bravo_changed",
            bravoMaxConn: 5,
            defaultConnStr: "Host=localhost;Database=default_stable"); // exact same value

        bool bravoCallbackFired = bravoFired.Wait(TimeSpan.FromSeconds(5));

        Assert.True(bravoCallbackFired,
            "Expected BRAVO change callback to fire within 5 seconds.");

        // Assert: BRAVO updated
        Assert.Equal("Host=new-server;Database=bravo_changed",
            bravoMonitor.CurrentValue.ConnectionString);

        // Assert: DEFAULT connection string is unchanged after the reload
        Assert.Equal("Host=localhost;Database=default_stable",
            defaultMonitor.CurrentValue.ConnectionString);
    }
}
