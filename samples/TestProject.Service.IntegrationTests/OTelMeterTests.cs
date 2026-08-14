namespace TestProject.Service.IntegrationTests;

/// <summary>
/// BTEL-05: Tests for the OTel counter site_profile_requests_total.
/// Verifies that each HTTP request through UseSiteProfileTelemetry increments the counter
/// with the correct site_id dimension.
/// </summary>
[Collection("OTelMeter")]
public sealed class OTelMeterTests
{
    /// <summary>
    /// Verifies that one request through UseSiteProfileTelemetry increments
    /// site_profile_requests_total by 1 with site_id = "ALPHA".
    /// </summary>
    [Fact]
    public async Task SiteProfileRequestsCounter_IncrementsPerRequest_WithSiteIdDimension()
    {
        // Arrange — capture measurements from the static counter.
        // Use delta approach: record snapshot before, assert delta after.
        var captured = new List<(long Value, string SiteId)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == SiteProfileTelemetryMetrics.MeterName &&
                instrument.Name == "site_profile_requests_total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name != "site_profile_requests_total") return;
            string siteId = "";
            foreach (var tag in tags)
            {
                if (tag.Key == "site_id")
                {
                    siteId = tag.Value?.ToString() ?? "";
                    break;
                }
            }
            captured.Add((measurement, siteId));
        });
        listener.Start();

        int initialCount = captured.Count;

        var server = BuildTestServer("ALPHA");
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        var newEntries = captured.Skip(initialCount).ToList();
        Assert.Single(newEntries);
        Assert.Equal(1L, newEntries[0].Value);
        Assert.Equal("ALPHA", newEntries[0].SiteId);
    }

    /// <summary>
    /// Verifies that 3 requests from 3 distinct sites produce 3 measurements
    /// with distinct site_id dimensions.
    /// </summary>
    [Fact]
    public async Task SiteProfileRequestsCounter_DistinctSites_ProduceDistinctDimensions()
    {
        // Arrange — shared measurement capture list.
        var captured = new List<(long Value, string SiteId)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == SiteProfileTelemetryMetrics.MeterName &&
                instrument.Name == "site_profile_requests_total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name != "site_profile_requests_total") return;
            string siteId = "";
            foreach (var tag in tags)
            {
                if (tag.Key == "site_id")
                {
                    siteId = tag.Value?.ToString() ?? "";
                    break;
                }
            }
            captured.Add((measurement, siteId));
        });
        listener.Start();

        int initialCount = captured.Count;

        // Use a mutable resolver so we can swap sites without creating new TestServers.
        var mutableResolver = new MutableSiteProfileResolver { SiteId = "ALPHA" };
        var server = BuildTestServerWithResolver(mutableResolver);
        var client = server.CreateClient();

        // Act — send 3 requests, each from a different site
        mutableResolver.SiteId = "ALPHA";
        await client.GetAsync("/");

        mutableResolver.SiteId = "BRAVO";
        await client.GetAsync("/");

        mutableResolver.SiteId = "DEFAULT";
        await client.GetAsync("/");

        // Assert — 3 new measurements with distinct site_id values
        var newEntries = captured.Skip(initialCount).ToList();
        Assert.Equal(3, newEntries.Count);

        var siteIds = newEntries.Select(e => e.SiteId).ToHashSet();
        Assert.Contains("ALPHA", siteIds);
        Assert.Contains("BRAVO", siteIds);
        Assert.Contains("DEFAULT", siteIds);

        // Each measurement should have value = 1 (per-request increment)
        Assert.All(newEntries, e => Assert.Equal(1L, e.Value));
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Simple mutable ISiteProfileResolver to swap site IDs within a single TestServer.
    /// </summary>
    private sealed class MutableSiteProfileResolver : ISiteProfileResolver
    {
        public string SiteId { get; set; } = "DEFAULT";
        public ISiteProfile Current => new FakeSiteProfile(SiteId);
    }

    private static TestServer BuildTestServer(string siteId) =>
        BuildTestServerWithResolver(new FakeSiteProfileResolver(siteId));

    private static TestServer BuildTestServerWithResolver(ISiteProfileResolver resolver)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                // SystemExecutionContextAccessor is required by MLog<T> constructor.
                services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
                services.AddSingleton(resolver);
                services.AddLogging(b => b.AddMuonroiLogging(useAsyncQueue: false));
            })
            .Configure(app =>
            {
                app.UseSiteProfileTelemetry();
                app.Run(ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    return Task.CompletedTask;
                });
            });

        return new TestServer(builder);
    }
}
