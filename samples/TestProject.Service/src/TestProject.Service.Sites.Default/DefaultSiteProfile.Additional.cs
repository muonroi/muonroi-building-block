using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;

namespace TestProject.Service.Sites.Default;

public partial class DefaultSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register default site-specific keyed services
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
    }
}
