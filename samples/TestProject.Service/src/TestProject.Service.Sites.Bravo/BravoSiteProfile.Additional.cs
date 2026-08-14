namespace TestProject.Service.Sites.Bravo;

public partial class BravoSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Bravo site-specific keyed services
        services.AddKeyedScoped<IOrderService, BravoOrderService>(SiteIds.BRAVO);

        // Register Bravo keyed command handler for MSiteCommandHandler keyed dispatch (SRVC-03)
        services.AddKeyedScoped<IRequestHandler<CreateOrderCommand, CreateOrderResponse>, BravoCreateOrderHandler>(SiteIds.BRAVO);
    }
}
