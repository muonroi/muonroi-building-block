using Grpc.Core;
using TestProject.Aggregate.Host.v1.Protos;

namespace TestProject.Aggregate.Host.v1.Services.Sites.Bravo;

/// <summary>
/// Bravo site-specific gRPC order service.
/// Inherits from <see cref="SharedOrderGrpcService"/> and overrides only the RPCs
/// that require Bravo-specific behavior.
///
/// <para>
/// This demonstrates the OOP inheritance pattern (D-03, D-04, D-16):
/// <list type="bullet">
///   <item>Inherits <see cref="SharedOrderGrpcService"/> — NOT <c>AggregateRpc.AggregateRpcBase</c> directly</item>
///   <item>Overrides <c>HandleContainer</c> with Bravo validation (BRV prefix check)</item>
///   <item>Does NOT override <c>ListContainers</c> — inherits shared implementation automatically</item>
///   <item>Uses <c>base.HandleContainer()</c> to delegate to shared logic after Bravo pre-processing</item>
/// </list>
/// </para>
///
/// <para>
/// Registered as the "BRAVO" keyed handler:
/// <code>
/// services.AddSiteGrpcHandler&lt;AggregateRpc.AggregateRpcBase, BravoOrderGrpcService&gt;("BRAVO");
/// </code>
/// </para>
/// </summary>
public class BravoOrderGrpcService : SharedOrderGrpcService
{
    /// <summary>
    /// Handles a container request with Bravo-specific pre-processing.
    ///
    /// Validates that the container number starts with "BRV" before delegating
    /// to the shared implementation via <c>base.HandleContainer()</c>.
    /// This proves: (1) override fires, (2) <c>base.Method()</c> delegates to shared logic.
    /// </summary>
    public override async Task<HandleContainerReply> HandleContainer(
        HandleContainerRequest request, ServerCallContext context)
    {
        // BRAVO pre-processing: validate container number prefix
        if (!request.ContainerNo.StartsWith("BRV", StringComparison.OrdinalIgnoreCase))
        {
            return new HandleContainerReply
            {
                Success = false,
                Message = "BRAVO requires BRV prefix"
            };
        }

        // Delegate to shared logic — base.HandleContainer returns "SHARED handled: {containerNo}"
        return await base.HandleContainer(request, context);
    }

    // NOTE: ListContainers is NOT overridden here.
    // It falls through to SharedOrderGrpcService.ListContainers via virtual dispatch.
    // This proves non-overridden RPCs work correctly in the inheritance chain.
}
