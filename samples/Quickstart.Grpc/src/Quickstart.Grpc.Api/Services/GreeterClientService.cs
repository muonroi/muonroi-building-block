namespace Quickstart.Grpc.Api.Services;

/// <summary>
/// A typed gRPC client service built on Muonroi.Grpc <see cref="BaseGrpcService"/>.
/// BaseGrpcService supplies CreateMetadata() (correlation/tenant/api-key propagation)
/// and CallGrpcServiceAsync() (retry + timeout + circuit-breaker + telemetry around a unary call).
/// </summary>
/// <remarks>
/// In a real service the lambda passed to CallGrpcServiceAsync would invoke a generated
/// gRPC stub (e.g. <c>greeterClient.SayHelloAsync(request, metadata)</c>). Here we stub the
/// transport with an in-process Task so the quickstart compiles without a .proto contract.
/// </remarks>
public sealed class GreeterClientService(ISystemExecutionContextAccessor contextAccessor)
    : BaseGrpcService(contextAccessor)
{
    /// <summary>
    /// Sends a greeting through the resilient gRPC call pipeline.
    /// </summary>
    public Task<string> SayHelloAsync(string name)
    {
        return CallGrpcServiceAsync(
            methodName: "Greeter/SayHello",
            grpcCall: (Metadata metadata) =>
            {
                // metadata carries correlation id, tenant id and api key from the
                // current execution context (see BaseGrpcService.CreateMetadata()).
                _ = metadata;
                return Task.FromResult($"Hello, {name}!");
            },
            policy: null);
    }
}
