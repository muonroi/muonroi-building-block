using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestProject.Aggregate.Core.Constants;
using TestProject.Aggregate.Core.Contracts;

namespace TestProject.Aggregate.Sites.Bravo;

public partial class BravoSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Bravo site-specific keyed handler
        services.AddKeyedScoped<IContainerHandler, BravoContainerHandler>(SiteIds.BRAVO);
    }
}
