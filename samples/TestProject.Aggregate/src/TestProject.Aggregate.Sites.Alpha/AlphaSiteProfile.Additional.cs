namespace TestProject.Aggregate.Sites.Alpha;

public partial class AlphaSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Alpha site-specific keyed handler
        services.AddKeyedScoped<IContainerHandler, AlphaContainerHandler>(SiteIds.ALPHA);
    }
}
