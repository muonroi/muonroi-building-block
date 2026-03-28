using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;

namespace TestProject.Service.Sites.Bravo;

public partial class BravoSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Bravo site-specific keyed services
        services.AddKeyedScoped<IOrderService, BravoOrderService>(SiteIds.BRAVO);
    }
}
