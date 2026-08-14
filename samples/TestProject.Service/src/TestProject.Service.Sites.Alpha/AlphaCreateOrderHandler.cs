namespace TestProject.Service.Sites.Alpha;

/// <summary>
/// Alpha site handler for CreateOrderCommand.
/// Registered as a keyed IRequestHandler under key "ALPHA" to enable
/// MSiteCommandHandler keyed dispatch via AddSiteCommandHandler.
/// </summary>
/// <inheritdoc />
public sealed class AlphaCreateOrderHandler(ISiteProfileResolver siteResolver, ISiteConfiguration siteConfig) : MSiteCommandHandler<CreateOrderCommand, CreateOrderResponse>(siteResolver, siteConfig)
{

    /// <inheritdoc />
    protected override Task<CreateOrderResponse> ExecuteAsync(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new CreateOrderResponse(
            Success: true,
            Message: $"ALPHA-Handled: {request.Name}",
            HandlerType: GetType().Name));
    }
}
