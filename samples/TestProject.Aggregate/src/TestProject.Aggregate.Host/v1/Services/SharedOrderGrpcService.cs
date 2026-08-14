namespace TestProject.Aggregate.Host.v1.Services;

/// <summary>
/// Shared gRPC service base for order/container handling.
/// All RPCs are declared as virtual so site-specific services can override them
/// via OOP inheritance. Acts as the "default" implementation.
///
/// <para>
/// This demonstrates the OOP inheritance pattern (D-04, D-16):
/// <list type="bullet">
///   <item>Inherits from <c>AggregateRpc.AggregateRpcBase</c> (proto-generated base)</item>
///   <item>All RPCs are <c>override virtual</c> — can be overridden at any depth</item>
///   <item>Registered as the "default" keyed handler via <c>AddSiteGrpcHandler(..., "default")</c></item>
/// </list>
/// </para>
///
/// <para>
/// Site-specific services inherit from this class and only override the RPCs
/// that need site-specific behavior. Non-overridden RPCs fall through to this
/// shared implementation automatically via virtual dispatch.
/// </para>
/// </summary>
public class SharedOrderGrpcService : AggregateRpc.AggregateRpcBase
{
    /// <summary>
    /// Handles a container request using shared default logic.
    /// Override is implicitly virtual — site-specific services can further override
    /// and call <c>base.HandleContainer()</c> to delegate to this shared logic.
    /// </summary>
    public override Task<HandleContainerReply> HandleContainer(
        HandleContainerRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HandleContainerReply
        {
            Success = true,
            Message = $"SHARED handled: {request.ContainerNo}"
        });
    }

    /// <summary>
    /// Lists containers using shared default logic.
    /// Override is implicitly virtual — site-specific services can further override as needed.
    /// </summary>
    public override Task<ListContainersReply> ListContainers(
        ListContainersRequest request, ServerCallContext context)
    {
        return Task.FromResult(new ListContainersReply
        {
            Total = 0
        });
    }
}
