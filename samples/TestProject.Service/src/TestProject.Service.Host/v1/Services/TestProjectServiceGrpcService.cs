using Grpc.Core;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;
using TestProject.Service.Host.v1.Protos;
using Muonroi.Tenancy.SiteProfile.Grpc;

namespace TestProject.Service.Host.v1.Services;

/// <summary>
/// gRPC service routing — dispatches to site-resolved IOrderService.
/// SiteCode extracted by SiteCodeGrpcInterceptor from configured metadata key.
/// </summary>
public sealed class TestProjectServiceGrpcService(
    IServiceProvider serviceProvider,
    ISiteCodeHolder siteCodeHolder) : ServiceRpc.ServiceRpcBase
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ISiteCodeHolder _siteCodeHolder = siteCodeHolder;

    public override async Task<CreateReply> Create(CreateRequest request, ServerCallContext context)
    {
        IOrderService service = ResolveSiteService();
        var result = await service.CreateAsync(request.Name, request.Description, context.CancellationToken);

        CreateReply reply = new()
        {
            Success = result.Success,
            Message = result.Message ?? "Created successfully"
        };
        reply.Details.AddRange(result.Details.Select(d => new OrderDetailInfo
        {
            OrderDetailNo = d.OrderDetailNo ?? string.Empty,
            ContainerNo = d.ContainerNo ?? string.Empty,
            Id = d.Id ?? string.Empty
        }));
        return reply;
    }

    public override async Task<GetByIdReply> GetById(GetByIdRequest request, ServerCallContext context)
    {
        IOrderService service = ResolveSiteService();
        var result = await service.GetByIdAsync(request.Id, context.CancellationToken);

        return new GetByIdReply
        {
            Id = result.Id,
            Name = result.Name ?? string.Empty,
            Description = result.Description ?? string.Empty,
            SiteId = _siteCodeHolder.SiteCode ?? SiteIds.DEFAULT
        };
    }

    public override async Task<GetListReply> GetList(GetListRequest request, ServerCallContext context)
    {
        IOrderService service = ResolveSiteService();
        var result = await service.GetListAsync(request.LinerCode, request.Page, request.PageSize, context.CancellationToken);

        GetListReply reply = new() { Total = result.Total };
        reply.Items.AddRange(result.Items.Select(i => new GetByIdReply
        {
            Id = i.Id,
            Name = i.Name ?? string.Empty,
            Description = i.Description ?? string.Empty,
            SiteId = _siteCodeHolder.SiteCode ?? SiteIds.DEFAULT
        }));
        return reply;
    }

    private IOrderService ResolveSiteService()
    {
        string siteCode = _siteCodeHolder.SiteCode ?? SiteIds.DEFAULT;
        return _serviceProvider.GetKeyedService<IOrderService>(siteCode)
            ?? _serviceProvider.GetRequiredKeyedService<IOrderService>(SiteIds.DEFAULT);
    }
}
