using Microsoft.Extensions.Configuration.Memory;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// INFR-06: Verifies that ISiteConfiguration picks up configuration changes at runtime.
/// Tests use two approaches:
///   1. In-memory IConfigurationRoot mutation — proves SiteConfiguration does NOT cache the section.
///   2. File-based reload with reloadOnChange:true — proves live appsettings.json changes are visible.
/// </summary>
public sealed class SiteConfigurationHotReloadTests
{
    [Fact]
    public void GetValue_AfterConfigMutation_ReturnsNewValue()
    {
        var logCapture = new LogCapture();

        // Build a mutable in-memory configuration source
        var source = new MemoryConfigurationSource
        {
            InitialData = new Dictionary<string, string?>
            {
                ["Sites:ALPHA:FeatureKey"] = "old-alpha-value"
            }
        };
        var config = new ConfigurationBuilder().Add(source).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<ISiteProfileResolver>(new FakeSiteProfileResolver("ALPHA"));
        services.AddLogging(b => b.AddProvider(logCapture));
        services.AddSiteConfiguration();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteConfigurationHotReloadTests>>();

        // Read old value — before mutation
        string? oldValue = siteConfig.GetValue<string>("FeatureKey");
        Assert.Equal("old-alpha-value", oldValue);
        logger.LogInformation("Before reload: FeatureKey = {Value} for site ALPHA", oldValue);

        // Mutate config at runtime (simulates hot-reload — IConfigurationRoot indexer update)
        ((IConfigurationRoot)config)["Sites:ALPHA:FeatureKey"] = "new-alpha-value";

        // Read new value — SiteConfiguration must NOT cache the section
        string? newValue = siteConfig.GetValue<string>("FeatureKey");
        Assert.Equal("new-alpha-value", newValue);
        logger.LogInformation("After reload: FeatureKey = {Value} for site ALPHA", newValue);

        // Log chain: both old and new values were observed
        Assert.True(logCapture.HasMessage("old-alpha-value"),
            "Expected log to contain 'old-alpha-value' before mutation");
        Assert.True(logCapture.HasMessage("new-alpha-value"),
            "Expected log to contain 'new-alpha-value' after mutation");
    }

    [Fact]
    public async Task GetValue_AfterFileChange_ReturnsUpdatedValue()
    {
        // Create a temp directory with an appsettings file
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string settingsPath = Path.Combine(tempDir, "appsettings.json");

        File.WriteAllText(settingsPath, """
        {
            "Sites": {
                "ALPHA": { "FeatureKey": "file-old-value" }
            }
        }
        """);

        try
        {
            var logCapture = new LogCapture();

            var config = new ConfigurationBuilder()
                .SetBasePath(tempDir)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton<ISiteProfileResolver>(new FakeSiteProfileResolver("ALPHA"));
            services.AddLogging(b => b.AddProvider(logCapture));
            services.AddSiteConfiguration();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfiguration>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteConfigurationHotReloadTests>>();

            // Read initial value
            string? before = siteConfig.GetValue<string>("FeatureKey");
            Assert.Equal("file-old-value", before);
            logger.LogInformation("Before file reload: FeatureKey = {Value}", before);

            // Register for the change token before modifying the file
            var reloadToken = ((IConfigurationRoot)config).GetReloadToken();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            reloadToken.RegisterChangeCallback(_ => tcs.TrySetResult(true), null);

            // Modify the file (write new JSON)
            File.WriteAllText(settingsPath, """
            {
                "Sites": {
                    "ALPHA": { "FeatureKey": "file-new-value" }
                }
            }
            """);

            // Wait for file watcher to detect change (up to 3 seconds)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // File watcher may not fire in CI — fall back to polling
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(200);
                    string? polled = siteConfig.GetValue<string>("FeatureKey");
                    if (polled == "file-new-value")
                        break;
                }
            }

            // Read updated value
            string? after = siteConfig.GetValue<string>("FeatureKey");
            Assert.Equal("file-new-value", after);
            logger.LogInformation("After file reload: FeatureKey = {Value}", after);

            // Log chain: both values were observed
            Assert.True(logCapture.HasMessage("file-old-value"),
                "Expected log to contain 'file-old-value'");
            Assert.True(logCapture.HasMessage("file-new-value"),
                "Expected log to contain 'file-new-value'");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
