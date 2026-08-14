WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;
IConfiguration configuration = builder.Configuration;

// gRPC site resolution — consumer defines which metadata key carries SiteCode
services.AddSiteGrpcServices(o =>
{
    o.MetadataKey = "x-site-code";
    o.HttpHeaderFallbackKey = "x-site-code";
    o.Required = false;
});
services.AddGrpc(o => o.Interceptors.Add<SiteCodeGrpcInterceptor>());

// Site-specific services — scans site assemblies for [GenerateSiteProfile] profiles
// Aggregate pattern: handler-based dispatch, no EF Core DbContext (SkipDbContextRegistration=true)
services.AddSiteServices(configuration,
    typeof(DefaultSiteProfile).Assembly,
    typeof(AlphaSiteProfile).Assembly,
    typeof(BravoSiteProfile).Assembly);

WebApplication app = builder.Build();

// Shared proto: aggregate gRPC service
app.MapGrpcService<TestProjectAggregateGrpcService>();

// Per-site proto: auto-discover [SiteGrpcService] and map as separate endpoints
app.MapSiteGrpcServices(typeof(BravoGrpcService).Assembly);

await app.RunAsync();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
