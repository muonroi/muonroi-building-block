#if (isAggregate)
using Muonroi.Tenancy.SiteProfile.Web;
#endif
#if (isService || isTos)
using MuonroiService.Core.Persistence;
using Muonroi.Tenancy.SiteProfile.Grpc;
#endif
#if (isService || isTos)
using MuonroiService.Host.Grpc;
#endif
#if (isDapper)
using Autofac;
using Autofac.Extensions.DependencyInjection;
#endif
using MuonroiService.Sites.Default;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;
IConfiguration configuration = builder.Configuration;

#if (isAggregate)
// Aggregate: REST controllers with per-site routing.
// AddSiteControllers calls AddControllers() internally and registers ApplicationParts.
services.AddSiteControllers(
    typeof(DefaultSiteProfile).Assembly);
#endif
#if (isService || isTos)
// Service/TOS: gRPC server with site-code interception
services.AddSiteGrpcServices(o =>
{
    o.MetadataKey = "SITE_CODE_METADATA_KEY";       // configurable via --siteCodeKey
    o.HttpHeaderFallbackKey = "SITE_CODE_METADATA_KEY"; // HTTP fallback
    o.Required = false;                              // set true when all clients send SiteCode
});
services.AddGrpc(o => o.Interceptors.Add<SiteCodeGrpcInterceptor>());
#endif

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

#if (isAggregate)
app.MapControllers();
#endif
#if (isService || isTos)
// Shared proto: default gRPC service
app.MapGrpcService<MuonroiServiceGrpcService>();

// Per-site proto: auto-discover [SiteGrpcService] and map as separate endpoints
// app.MapSiteGrpcServices(typeof(XxxGrpcService).Assembly);
#endif

await app.RunAsync();
