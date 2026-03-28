using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestProject.Aggregate.Core.Constants;
using TestProject.Aggregate.Core.Contracts;

namespace TestProject.Aggregate.Sites.Alpha;

public partial class AlphaSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Alpha site-specific keyed handler
        services.AddKeyedScoped<IContainerHandler, AlphaContainerHandler>(SiteIds.ALPHA);
    }
}
