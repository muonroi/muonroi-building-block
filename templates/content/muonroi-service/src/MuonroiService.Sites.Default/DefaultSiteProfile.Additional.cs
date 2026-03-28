using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MuonroiService.Core.Constants;
using MuonroiService.Core.Contracts;

namespace MuonroiService.Sites.Default;

public partial class DefaultSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register default site-specific keyed services
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
    }
}
