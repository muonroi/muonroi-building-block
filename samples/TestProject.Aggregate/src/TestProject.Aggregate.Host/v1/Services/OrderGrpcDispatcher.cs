using Grpc.Core;
using Muonroi.Tenancy.SiteProfile.Grpc;
using TestProject.Aggregate.Host.v1.Protos;

namespace TestProject.Aggregate.Host.v1.Services;

/// <summary>
/// Dispatching proxy for the <c>AggregateRpc</c> gRPC service.
/// Registered via <c>app.MapGrpcService&lt;OrderGrpcDispatcher&gt;()</c> as the single
/// endpoint for the shared proto. Delegates each RPC to the site-specific handler
/// resolved by <see cref="SiteGrpcDispatchHelper{TServiceBase}"/>.
///
/// <para>
/// This demonstrates the dispatching proxy pattern (D-05, D-06, D-16):
/// <list type="bullet">
///   <item>One line per RPC — <c>~5 lines total per method</c></item>
///   <item>No manual site resolution — <see cref="SiteGrpcDispatchHelper{TServiceBase}"/> handles it</item>
///   <item>No switch/if on SiteCode — resolution is opaque to the dispatcher</item>
///   <item>Falls back to "default" handler if no site-specific handler is registered</item>
/// </list>
/// </para>
///
/// <para>
/// Registration in Program.cs:
/// <code>
/// services.AddSiteGrpcHandler&lt;AggregateRpc.AggregateRpcBase, SharedOrderGrpcService&gt;("default");
/// services.AddSiteGrpcHandler&lt;AggregateRpc.AggregateRpcBase, BravoOrderGrpcService&gt;("BRAVO");
/// services.AddSiteGrpcDispatcher&lt;AggregateRpc.AggregateRpcBase&gt;();
///
/// app.MapGrpcService&lt;OrderGrpcDispatcher&gt;();
/// </code>
/// </para>
/// </summary>
public class OrderGrpcDispatcher : AggregateRpc.AggregateRpcBase
{
    private readonly SiteGrpcDispatchHelper<AggregateRpc.AggregateRpcBase> _helper;

    /// <summary>Creates a new <see cref="OrderGrpcDispatcher"/> with the site dispatch helper.</summary>
    public OrderGrpcDispatcher(SiteGrpcDispatchHelper<AggregateRpc.AggregateRpcBase> helper)
        => _helper = helper;

    /// <inheritdoc/>
    public override Task<HandleContainerReply> HandleContainer(
        HandleContainerRequest request, ServerCallContext context)
        => _helper.DispatchAsync(context, (handler, ctx) => handler.HandleContainer(request, ctx));

    /// <inheritdoc/>
    public override Task<ListContainersReply> ListContainers(
        ListContainersRequest request, ServerCallContext context)
        => _helper.DispatchAsync(context, (handler, ctx) => handler.ListContainers(request, ctx));
}
