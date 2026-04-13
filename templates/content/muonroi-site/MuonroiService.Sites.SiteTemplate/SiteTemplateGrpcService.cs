using Grpc.Core;
using Muonroi.Tenancy.SiteProfile.Grpc;
using MuonroiService.Sites.SiteTemplate.Protos;

namespace MuonroiService.Sites.SiteTemplate;

/// <summary>
/// SITE_ID_UPPER per-site gRPC service — handles RPCs from service.SITE_ID_LOWER.proto.
/// Only included when protoMode=perSite.
///
/// Registration in Host Program.cs:
///   app.MapSiteGrpcServices(typeof(SiteTemplateGrpcService).Assembly);
///
/// Callers reference service.SITE_ID_LOWER.proto to talk to this endpoint.
/// </summary>
[SiteGrpcService("SITE_ID_UPPER", Reason = "SITE_ID_UPPER has unique RPCs not in shared proto")]
public class SiteTemplateGrpcService : SiteTemplateMuonroiServiceRpc.SiteTemplateMuonroiServiceRpcBase
{
    // TODO: Inject site-specific services via constructor

    public override Task<SiteTemplateCreateReply> CreateWithValidation(
        SiteTemplateCreateRequest request, ServerCallContext context)
    {
        // TODO: Implement SITE_ID_UPPER-specific creation logic
        return Task.FromResult(new SiteTemplateCreateReply
        {
            Success = true,
            Message = $"SITE_ID_UPPER order created: {request.OrderId}"
        });
    }

    public override Task<SiteTemplateGetDetailsReply> GetDetails(
        SiteTemplateGetDetailsRequest request, ServerCallContext context)
    {
        // TODO: Implement SITE_ID_UPPER-specific details query
        return Task.FromResult(new SiteTemplateGetDetailsReply());
    }
}
