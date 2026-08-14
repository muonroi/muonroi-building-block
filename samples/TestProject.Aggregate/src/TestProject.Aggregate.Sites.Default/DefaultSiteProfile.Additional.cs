namespace TestProject.Aggregate.Sites.Default;

public partial class DefaultSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Default site-specific keyed handler
        services.AddKeyedScoped<IContainerHandler, DefaultContainerHandler>(SiteIds.DEFAULT);
    }
}
