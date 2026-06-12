using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MuonroiService.Core.Constants;
using MuonroiService.Core.Contracts;

namespace MuonroiService.Sites.Default;

public partial class DefaultSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
#if (isAggregate)
        // Aggregate: register the default site handler and service proxy
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
#endif
#if (isService)
        // Service: register service implementation (backed by EF or Dapper)
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
#endif
#if (isTos)
        // TOS: register read-only service implementation (Dapper-only, no EF)
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
#endif
    }
}
