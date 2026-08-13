using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;
using Muonroi.Tenancy.SiteProfile.Web.Telemetry;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// BTEL-04: Tests for UseSiteProfileTelemetry middleware — Activity tag enrichment and IMLog scope enrichment.
/// Placed in the same collection as OTelMeterTests to prevent parallel meter listener interference.
/// </summary>
[Collection("OTelMeter")]
public sealed class TelemetryMiddlewareTests
{
    /// <summary>
    /// Verifies that UseSiteProfileTelemetry tags Activity.Current with site.id == the active site's SiteId.
    /// </summary>
    [Fact]
    public async Task UseSiteProfileTelemetry_TagsActivityWithSiteId()
    {
        // Arrange
        string? capturedSiteTag = null;

        // We need an active Activity so Activity.Current is not null when middleware runs.
        // Register an ActivityListener to ensure activities are sampled.
        using var activitySource = new ActivitySource("test.source");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        var server = BuildTestServer(
            services =>
            {
                // SystemExecutionContextAccessor is required by MLog<T> constructor.
                services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
                services.AddSingleton<ISiteProfileResolver>(new FakeSiteProfileResolver("ALPHA"));
                services.AddLogging(b => b.AddMuonroiLogging(useAsyncQueue: false));
            },
            app =>
            {
                app.UseSiteProfileTelemetry();
                app.Run(ctx =>
                {
                    capturedSiteTag = Activity.Current?.GetTagItem("site.id")?.ToString();
                    ctx.Response.StatusCode = 200;
                    return Task.CompletedTask;
                });
            });

        var client = server.CreateClient();

        // Start an activity so Activity.Current is non-null for the test pipeline
        using var activity = activitySource.StartActivity("test-op");

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("ALPHA", capturedSiteTag);
    }

    /// <summary>
    /// Verifies that UseSiteProfileTelemetry enriches the IMLog scope with SiteId == active site's SiteId.
    /// </summary>
    [Fact]
    public async Task UseSiteProfileTelemetry_EnrichesLogScopeWithSiteId()
    {
        // Arrange
        var logCapture = new LogCapture();

        var server = BuildTestServer(
            services =>
            {
                // SystemExecutionContextAccessor is required by MLog<T> constructor.
                services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
                services.AddSingleton<ISiteProfileResolver>(new FakeSiteProfileResolver("ALPHA"));
                services.AddLogging(b =>
                {
                    b.AddMuonroiLogging(useAsyncQueue: false);
                    b.AddProvider(logCapture);
                    b.SetMinimumLevel(LogLevel.Trace);
                });
            },
            app =>
            {
                app.UseSiteProfileTelemetry();
                app.Run(async ctx =>
                {
                    // Write a log entry via the SAME category that UseSiteProfileTelemetry uses
                    // (IMLog<SiteProfileTelemetryMiddleware>), so the CaptureLogger for that category
                    // records the entry with the active SiteId scope.
                    var logger = ctx.RequestServices.GetRequiredService<IMLog<SiteProfileTelemetryMiddleware>>();
                    logger.Info("test-scope-check");
                    ctx.Response.StatusCode = 200;
                    await Task.CompletedTask;
                });
            });

        var client = server.CreateClient();

        // Act
        await client.GetAsync("/");

        // Assert — find the log entry written in the terminal handler
        var entry = logCapture.FindEntries("test-scope-check").FirstOrDefault();
        Assert.NotNull(entry);
        Assert.NotNull(entry.Scope);
        Assert.True(entry.Scope!.ContainsKey("SiteId"), "Expected 'SiteId' key in log scope");
        Assert.Equal("ALPHA", entry.Scope["SiteId"]?.ToString());
    }

    /// <summary>
    /// Verifies that different sites produce different Activity tag values.
    /// </summary>
    [Fact]
    public async Task UseSiteProfileTelemetry_DifferentSites_ProduceDifferentTags()
    {
        // Arrange
        string? tagAlpha = null;
        string? tagBravo = null;

        var serverAlpha = BuildTestServer(
            services =>
            {
                // SystemExecutionContextAccessor is required by MLog<T> constructor.
                services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
                services.AddSingleton<ISiteProfileResolver>(new FakeSiteProfileResolver("ALPHA"));
                services.AddLogging(b => b.AddMuonroiLogging(useAsyncQueue: false));
            },
            app =>
            {
                app.UseSiteProfileTelemetry();
                app.Run(ctx =>
                {
                    tagAlpha = Activity.Current?.GetTagItem("site.id")?.ToString();
                    ctx.Response.StatusCode = 200;
                    return Task.CompletedTask;
                });
            });

        var serverBravo = BuildTestServer(
            services =>
            {
                // SystemExecutionContextAccessor is required by MLog<T> constructor.
                services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
                services.AddSingleton<ISiteProfileResolver>(new FakeSiteProfileResolver("BRAVO"));
                services.AddLogging(b => b.AddMuonroiLogging(useAsyncQueue: false));
            },
            app =>
            {
                app.UseSiteProfileTelemetry();
                app.Run(ctx =>
                {
                    tagBravo = Activity.Current?.GetTagItem("site.id")?.ToString();
                    ctx.Response.StatusCode = 200;
                    return Task.CompletedTask;
                });
            });

        // Register listener to ensure activities are sampled
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        await serverAlpha.CreateClient().GetAsync("/");
        await serverBravo.CreateClient().GetAsync("/");

        // Assert
        Assert.Equal("ALPHA", tagAlpha);
        Assert.Equal("BRAVO", tagBravo);
        Assert.NotEqual(tagAlpha, tagBravo);
    }

    // ---------------------------------------------------------------------------
    // Private factory helper
    // ---------------------------------------------------------------------------

    private static TestServer BuildTestServer(
        Action<IServiceCollection> configureServices,
        Action<IApplicationBuilder> configureApp)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(configureServices)
            .Configure(configureApp);

        return new TestServer(builder);
    }
}
