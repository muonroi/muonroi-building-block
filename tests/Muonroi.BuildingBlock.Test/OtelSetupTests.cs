using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Muonroi.Observability;

namespace Muonroi.BuildingBlock.Test;

public class OtelSetupTests
{
    [Fact]
    public void AddObservability_Registers_Providers_And_Enrichers()
    {
        Dictionary<string, string?> settings = new()
        {
            ["OpenTelemetry:ServiceName"] = "TestService",
            ["OpenTelemetry:OtlpEndpoint"] = "http://localhost:4317"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        ServiceCollection services = [];
        services.AddLogging();
        services.AddObservability(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        using TracerProvider tracerProvider = provider.GetRequiredService<TracerProvider>();
        using MeterProvider meterProvider = provider.GetRequiredService<MeterProvider>();

        Assert.NotNull(tracerProvider);
        Assert.NotNull(meterProvider);

        string? previousTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = "tenant-123";
            ActivitySource source = new("MassTransit");
            using Activity? activity = source.StartActivity("TestActivity");

            Assert.NotNull(activity);
            Assert.Equal("tenant-123", activity!.GetTagItem("tenant.id"));
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
        }
    }
}
