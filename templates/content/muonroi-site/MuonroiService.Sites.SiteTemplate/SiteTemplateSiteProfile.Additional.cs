using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MuonroiService.Sites.SiteTemplate;

public partial class SiteTemplateSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register SiteTemplate site-specific keyed services
        services.AddKeyedScoped<IOrderService, SiteTemplateOrderService>("SITE_ID_UPPER");
    }
}
