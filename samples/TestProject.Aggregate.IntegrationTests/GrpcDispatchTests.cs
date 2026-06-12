using Grpc.Core;
using Grpc.Net.Client;
using TestProject.Aggregate.Host.v1.Protos;
using TestProject.Aggregate.IntegrationTests.Helpers;
using TestProject.Aggregate.Sites.Bravo.Protos;

namespace TestProject.Aggregate.IntegrationTests;

/// <summary>
/// Integration tests verifying per-site gRPC dispatch via the Aggregate Host.
///
/// These tests use WebApplicationFactory to host TestProject.Aggregate.Host in-process,
/// send gRPC requests with x-site-code metadata, and assert that per-site handlers
/// return site-specific responses. Covers GRPC-01.
/// </summary>
public sealed class GrpcDispatchTests(AggregateWebAppFactory factory) : IClassFixture<AggregateWebAppFactory>
{
    private readonly AggregateWebAppFactory _factory = factory;

    /// <summary>
    /// GRPC-01: Shared endpoint with BRAVO site context.
    ///
    /// The shared AggregateRpc.HandleContainer endpoint reads x-site-code=BRAVO from gRPC
    /// metadata via SiteCodeGrpcInterceptor → ISiteCodeHolder, then routes to
    /// BravoContainerHandler (keyed "BRAVO") which returns "BRAVO-Handled {containerNo}".
    /// </summary>
    [Fact]
    public async Task HandleContainer_WithBravoContext_ReturnsBravoResponse()
    {
        // Arrange
        using GrpcChannel channel = _factory.CreateGrpcChannel();
        AggregateRpc.AggregateRpcClient client = new(channel);

        Metadata headers = new() { { "x-site-code", "BRAVO" } };
        HandleContainerRequest request = new() { ContainerNo = "BRAVO-C001", IsoCode = "22G1" };

        // Act
        HandleContainerReply reply = await client.HandleContainerAsync(request, headers: headers);

        // Assert: BravoContainerHandler.HandleAsync returns "BRAVO-Handled {containerNo}"
        reply.Success.Should().BeTrue(because: "BravoContainerHandler.HandleAsync always returns Success=true");
        reply.Message.Should().Contain("BRAVO",
            because: "x-site-code=BRAVO routes to BravoContainerHandler which prefixes message with 'BRAVO-Handled'");
    }

    /// <summary>
    /// GRPC-01: Shared endpoint with ALPHA site context.
    ///
    /// Proves dispatch is not hard-coded to BRAVO. With x-site-code=ALPHA,
    /// the handler resolves AlphaContainerHandler (keyed "ALPHA") which returns "ALPHA-Handled {containerNo}".
    /// </summary>
    [Fact]
    public async Task HandleContainer_WithAlphaContext_ReturnsAlphaResponse()
    {
        // Arrange
        using GrpcChannel channel = _factory.CreateGrpcChannel();
        AggregateRpc.AggregateRpcClient client = new(channel);

        Metadata headers = new() { { "x-site-code", "ALPHA" } };
        HandleContainerRequest request = new() { ContainerNo = "ALPHA-C001", IsoCode = "22G1" };

        // Act
        HandleContainerReply reply = await client.HandleContainerAsync(request, headers: headers);

        // Assert: AlphaContainerHandler.HandleAsync returns "ALPHA-Handled {containerNo}"
        reply.Success.Should().BeTrue(because: "AlphaContainerHandler.HandleAsync always returns Success=true");
        reply.Message.Should().Contain("ALPHA",
            because: "x-site-code=ALPHA routes to AlphaContainerHandler which prefixes message with 'ALPHA-Handled'");
        reply.Message.Should().NotContain("BRAVO",
            because: "ALPHA context must not route to the BRAVO handler — proving dispatch is not hard-coded");
    }

    /// <summary>
    /// GRPC-01: Per-site Bravo proto endpoint (BravoAggregateRpc.HandleWithValidation).
    ///
    /// BravoGrpcService is registered via app.MapSiteGrpcServices(typeof(BravoGrpcService).Assembly)
    /// and is only accessible when x-site-code=BRAVO is present. The endpoint returns
    /// "BRAVO validated: {containerNo}" — Bravo-specific behavior not in the shared proto.
    /// </summary>
    [Fact]
    public async Task HandleWithValidation_BravoPerSiteEndpoint_ReturnsBravoValidatedResponse()
    {
        // Arrange
        using GrpcChannel channel = _factory.CreateGrpcChannel();
        BravoAggregateRpc.BravoAggregateRpcClient client = new(channel);

        Metadata headers = new() { { "x-site-code", "BRAVO" } };
        BravoHandleRequest request = new() { ContainerNo = "BRAVO-V001", IsoCode = "22G1" };

        // Act
        BravoHandleReply reply = await client.HandleWithValidationAsync(request, headers: headers);

        // Assert: BravoGrpcService.HandleWithValidation returns "BRAVO validated: {containerNo}"
        reply.Success.Should().BeTrue(because: "BravoGrpcService.HandleWithValidation always returns Success=true");
        reply.Message.Should().Contain("BRAVO validated",
            because: "The per-site Bravo proto endpoint BravoAggregateRpc.HandleWithValidation " +
                     "returns 'BRAVO validated: {containerNo}' — this is Bravo-specific behavior " +
                     "not available in the shared AggregateRpc service");
    }
}
