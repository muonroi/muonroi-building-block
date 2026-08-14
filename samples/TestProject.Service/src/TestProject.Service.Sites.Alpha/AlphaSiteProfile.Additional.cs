namespace TestProject.Service.Sites.Alpha;

public partial class AlphaSiteProfile
{
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Alpha site-specific keyed services
        services.AddKeyedScoped<IOrderService, AlphaOrderService>(SiteIds.ALPHA);

        // Register Alpha keyed command handler for MSiteCommandHandler keyed dispatch (SRVC-03)
        services.AddKeyedScoped<IRequestHandler<CreateOrderCommand, CreateOrderResponse>, AlphaCreateOrderHandler>(SiteIds.ALPHA);
    }
}
