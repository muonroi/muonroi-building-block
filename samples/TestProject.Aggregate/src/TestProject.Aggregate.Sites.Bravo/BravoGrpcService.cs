using Grpc.Core;
using Muonroi.Tenancy.SiteProfile.Grpc;
using TestProject.Aggregate.Sites.Bravo.Protos;

namespace TestProject.Aggregate.Sites.Bravo;

/// <summary>
/// Bravo per-site gRPC service — handles RPCs from service.bravo.proto.
/// Registered via: app.MapSiteGrpcServices(typeof(BravoGrpcService).Assembly);
/// </summary>
[SiteGrpcService("BRAVO", Reason = "Bravo has unique container validation RPCs")]
public class BravoGrpcService : BravoAggregateRpc.BravoAggregateRpcBase
{
    public override Task<BravoHandleReply> HandleWithValidation(
        BravoHandleRequest request, ServerCallContext context)
    {
        return Task.FromResult(new BravoHandleReply
        {
            Success = true,
            Message = $"BRAVO validated: {request.ContainerNo}"
        });
    }

    public override Task<BravoListReply> GetBravoContainers(
        BravoListRequest request, ServerCallContext context)
    {
        var reply = new BravoListReply { Total = 1 };
        reply.Items.Add(new Host.v1.Protos.SharedContainerInfo
        {
            ContainerNo = "BRAVO001",
            IsoCode = "22G1",
            Status = "Active",
            CreatedDate = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
        });
        return Task.FromResult(reply);
    }
}
