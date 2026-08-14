namespace TestProject.Aggregate.Sites.Bravo;

public partial class BravoSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Bravo site-specific keyed handler
        services.AddKeyedScoped<IContainerHandler, BravoContainerHandler>(SiteIds.BRAVO);
    }
}
