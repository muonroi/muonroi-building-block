using TestProject.Service.Core.Infrastructure;
using TestProject.Service.Host.v1.Services;
using TestProject.Service.Sites.Default;
using TestProject.Service.Sites.Alpha;
using TestProject.Service.Sites.Bravo;
using Muonroi.Tenancy.SiteProfile.Grpc;

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
services.AddSiteServices(configuration,
    typeof(DefaultSiteProfile).Assembly,
    typeof(AlphaSiteProfile).Assembly,
    typeof(BravoSiteProfile).Assembly);

WebApplication app = builder.Build();

// Shared proto: default gRPC service
app.MapGrpcService<TestProjectServiceGrpcService>();

await app.RunAsync();
