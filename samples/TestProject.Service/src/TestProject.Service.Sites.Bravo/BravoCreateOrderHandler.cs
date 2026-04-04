using Muonroi.Tenancy.SiteProfile;
using Muonroi.Tenancy.SiteProfile.Web.Configuration;
using Muonroi.Tenancy.SiteProfile.Web.Handlers;
using TestProject.Service.Core.Contracts;

namespace TestProject.Service.Sites.Bravo;

/// <summary>
/// Bravo site handler for CreateOrderCommand.
/// Registered as a keyed IRequestHandler under key "BRAVO" to enable
/// MSiteCommandHandler keyed dispatch via AddSiteCommandHandler.
/// </summary>
/// <inheritdoc />
public sealed class BravoCreateOrderHandler(ISiteProfileResolver siteResolver, ISiteConfiguration siteConfig) : MSiteCommandHandler<CreateOrderCommand, CreateOrderResponse>(siteResolver, siteConfig)
{

    /// <inheritdoc />
    protected override Task<CreateOrderResponse> ExecuteAsync(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new CreateOrderResponse(
            Success: true,
            Message: $"BRAVO-Handled: {request.Name}",
            HandlerType: GetType().Name));
    }
}
