using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using TestProject.Aggregate.Core.Contracts;
using TestProject.Aggregate.Host.v1.Protos;
using Google.Protobuf.WellKnownTypes;

namespace TestProject.Aggregate.Host.v1.Services;

/// <summary>
/// Shared gRPC service implementation for the Aggregate host.
/// Dispatches to the site-specific IContainerHandler keyed service.
/// </summary>
public class TestProjectAggregateGrpcService : AggregateRpc.AggregateRpcBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISiteCodeHolder _siteCodeHolder;

    public TestProjectAggregateGrpcService(
        IServiceProvider serviceProvider,
        ISiteCodeHolder siteCodeHolder)
    {
        _serviceProvider = serviceProvider;
        _siteCodeHolder = siteCodeHolder;
    }

    public override async Task<HandleContainerReply> HandleContainer(
        HandleContainerRequest request, ServerCallContext context)
    {
        string siteCode = _siteCodeHolder.SiteCode ?? "DEFAULT";
        var handler = _serviceProvider.GetKeyedService<IContainerHandler>(siteCode)
            ?? _serviceProvider.GetRequiredService<IContainerHandler>();

        var result = await handler.HandleAsync(request.ContainerNo, request.IsoCode, context.CancellationToken);
        return new HandleContainerReply
        {
            Success = result.Success,
            Message = result.Message ?? string.Empty
        };
    }

    public override async Task<ListContainersReply> ListContainers(
        ListContainersRequest request, ServerCallContext context)
    {
        string siteCode = _siteCodeHolder.SiteCode ?? "DEFAULT";
        var handler = _serviceProvider.GetKeyedService<IContainerHandler>(siteCode)
            ?? _serviceProvider.GetRequiredService<IContainerHandler>();

        var result = await handler.ListAsync(request.BillNo, context.CancellationToken);
        var reply = new ListContainersReply { Total = result.Total };
        foreach (var item in result.Items)
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
