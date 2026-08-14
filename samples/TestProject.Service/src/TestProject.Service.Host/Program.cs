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
// CHARLIE added: alias for DEFAULT (uses DEFAULT service registrations via [SiteProfileAlias])
services.AddSiteServices(configuration,
    typeof(DefaultSiteProfile).Assembly,
    typeof(AlphaSiteProfile).Assembly,
    typeof(BravoSiteProfile).Assembly,
    typeof(CharlieSiteProfile).Assembly);

// MSitePipeline infrastructure — hook-based alternative to virtual override pattern (D-27)
services.AddSitePipeline();

// BRAVO order creation pipeline hooks:
// Before hook: validates BookingNo is present (BRAVO business rule)
services.AddSiteStepHook<IOrderService>("BRAVO", "create-order", SiteStepHookPhase.Before,
    _ => new BravoValidateOrderHook());

// After hook: enriches FactBag with BRAVO post-processing markers
services.AddSiteStepHook<IOrderService>("BRAVO", "create-order", SiteStepHookPhase.After,
    _ => new BravoEnrichOrderHook());

// BRAVO custom column map — BOOKING_NUMBER instead of BOOKING_NO convention
// Register as keyed singleton; call AddSiteResolvedService<ISiteColumnMap>() to enable dispatch factory
services.AddKeyedSingleton<ISiteColumnMap, BravoColumnMap>("BRAVO");

// Schema validation at startup — WarnOnMismatch logs mismatches without stopping the service (D-17)
services.AddSiteSchemaValidation(o => o.Severity = SchemaValidationSeverity.WarnOnMismatch);

WebApplication app = builder.Build();

// Shared proto: default gRPC service
app.MapGrpcService<TestProjectServiceGrpcService>();

await app.RunAsync();
