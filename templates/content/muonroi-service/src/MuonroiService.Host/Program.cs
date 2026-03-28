using MuonroiService.Core.Infrastructure;
using MuonroiService.Host.v1.Services;
using MuonroiService.Sites.Default;
using Muonroi.Tenancy.SiteProfile.Grpc;
#if (isDapper)
using Autofac;
using Autofac.Extensions.DependencyInjection;
#endif

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;
IConfiguration configuration = builder.Configuration;

// gRPC site resolution — consumer defines which metadata key carries SiteCode
services.AddSiteGrpcServices(o =>
{
    o.MetadataKey = "SITE_CODE_METADATA_KEY";       // configurable via --siteCodeKey
    o.HttpHeaderFallbackKey = "SITE_CODE_METADATA_KEY"; // HTTP fallback
    o.Required = false;                              // set true when all clients send SiteCode
});
services.AddGrpc(o => o.Interceptors.Add<SiteCodeGrpcInterceptor>());

// Site-specific services — scans site assemblies for [GenerateSiteProfile] profiles
// Add new site assemblies here after creating via: dotnet new tenant-site-module
services.AddSiteServices(configuration,
    typeof(DefaultSiteProfile).Assembly);

#if (isEf)
// EF Core: per-site DbContext registration handled by [GenerateSiteProfile] source generator.
// Each site's RegisterServices() calls AddSiteDbContext<TSiteContext>().
#endif
#if (isDapper)
// Dapper: Autofac container with per-site named repository resolution.
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(SiteServiceResolver.Configure);
#endif

WebApplication app = builder.Build();

// Shared proto: default gRPC service
app.MapGrpcService<MuonroiServiceGrpcService>();

// Per-site proto: auto-discover [SiteGrpcService] and map as separate endpoints
// app.MapSiteGrpcServices(typeof(XxxGrpcService).Assembly);

await app.RunAsync();
