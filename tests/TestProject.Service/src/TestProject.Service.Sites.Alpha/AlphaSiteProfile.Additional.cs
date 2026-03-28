using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;

namespace TestProject.Service.Sites.Alpha;

public partial class AlphaSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Alpha site-specific keyed services
        services.AddKeyedScoped<IOrderService, AlphaOrderService>(SiteIds.ALPHA);
    }
}
