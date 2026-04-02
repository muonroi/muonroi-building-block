

namespace TestProject.Aggregate.Host.v1.Services;

/// <summary>
/// Shared gRPC service implementation for the Aggregate host.
/// Dispatches to the site-specific IContainerHandler keyed service.
/// </summary>
public class TestProjectAggregateGrpcService(
    IServiceProvider serviceProvider,
    ISiteCodeHolder siteCodeHolder) : AggregateRpc.AggregateRpcBase
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ISiteCodeHolder _siteCodeHolder = siteCodeHolder;

    /// <inheritdoc/>
    public override async Task<HandleContainerReply> HandleContainer(
        HandleContainerRequest request, ServerCallContext context)
    {
        string siteCode = _siteCodeHolder.SiteCode ?? "DEFAULT";
        IContainerHandler handler = _serviceProvider.GetKeyedService<IContainerHandler>(siteCode)
            ?? _serviceProvider.GetRequiredService<IContainerHandler>();

        ContainerResult result = await handler.HandleAsync(request.ContainerNo, request.IsoCode, context.CancellationToken);
        return new HandleContainerReply
        {
            Success = result.Success,
            Message = result.Message ?? string.Empty
        };
    }

    /// <inheritdoc/>
    public override async Task<ListContainersReply> ListContainers(
        ListContainersRequest request, ServerCallContext context)
    {
        string siteCode = _siteCodeHolder.SiteCode ?? "DEFAULT";
        IContainerHandler handler = _serviceProvider.GetKeyedService<IContainerHandler>(siteCode)
            ?? _serviceProvider.GetRequiredService<IContainerHandler>();

        ContainerListResult result = await handler.ListAsync(request.BillNo, context.CancellationToken);
        var reply = new ListContainersReply { Total = result.Total };
        foreach (ContainerItem item in result.Items)
        {
            reply.Items.Add(new SharedContainerInfo
            {
                ContainerNo = item.ContainerNo,
                IsoCode = item.IsoCode,
                Status = item.Status,
                CreatedDate = Timestamp.FromDateTime(DateTime.UtcNow)
            });
        }
        return reply;
    }
}
