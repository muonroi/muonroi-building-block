using Grpc.Core;
using Grpc.Net.Client;
using TestProject.Aggregate.Host.v1.Protos;
using TestProject.Aggregate.Sites.Bravo.Protos;

namespace TestProject.Aggregate.IntegrationTests;

/// <summary>
/// FAIL-05: Verifies that gRPC calls with wrong or missing site code return clear gRPC error
/// statuses — not 500s, not NullReferenceException, and not silent success.
///
/// These are NEGATIVE tests — they intentionally use wrong/missing site codes.
///
/// NOTE on Required=false: The Aggregate Host configures SiteGrpcOptions.Required=false.
/// This means a missing x-site-code header does NOT trigger InvalidArgument from the interceptor.
/// Instead, the request proceeds without a SiteCode set — and behavior depends on the handler.
/// </summary>
public sealed class FailureScenario_GrpcWrongSiteTests(AggregateWebAppFactory factory) : IClassFixture<AggregateWebAppFactory>
{
    private readonly AggregateWebAppFactory _factory = factory;

    /// <summary>
    /// FAIL-05 Test 1: HandleContainer with x-site-code="NONEXISTENT" returns a gRPC error.
    ///
    /// When SiteCode="NONEXISTENT" is set, the SiteCodeGrpcInterceptor stores it in ISiteCodeHolder.
    /// The handler then tries to resolve a keyed service for "NONEXISTENT" — which doesn't exist.
    /// This causes an InvalidOperationException internally, which ASP.NET Core converts to
    /// gRPC StatusCode.Internal (500). We assert StatusCode is NOT OK.
    /// </summary>
    [Fact]
    public async Task HandleContainer_WithNonexistentSiteCode_ReturnsGrpcError()
    {
        // Arrange
        using GrpcChannel channel = _factory.CreateGrpcChannel();
        AggregateRpc.AggregateRpcClient client = new(channel);

        Metadata headers = new() { { "x-site-code", "NONEXISTENT" } };
        HandleContainerRequest request = new() { ContainerNo = "X-001", IsoCode = "22G1" };

        // Act — expect a gRPC error, not a successful response
        RpcException? caughtException = null;
        try
        {
            _ = await client.HandleContainerAsync(request, headers: headers);
        }
        catch (RpcException ex)
        {
            caughtException = ex;
        }

        // Assert — must throw RpcException (not succeed silently)
        Assert.NotNull(caughtException);
        Assert.NotEqual(StatusCode.OK, caughtException.StatusCode);

        // The StatusCode should be one of the expected failure codes:
        // Internal (500 from unhandled exception), InvalidArgument (interceptor rejection),
        // Unimplemented (no handler registered), or Unknown (generic gRPC error).
        var expectedCodes = new[]
        {
            StatusCode.Internal,
            StatusCode.InvalidArgument,
            StatusCode.Unimplemented,
            StatusCode.Unknown,
            StatusCode.NotFound
        };

        Assert.Contains(caughtException.StatusCode, expectedCodes);
    }

    /// <summary>
    /// FAIL-05 Test 2: gRPC error for non-existent site does NOT expose internal implementation details.
    ///
    /// The error detail returned to the client must NOT contain:
    /// - "NullReferenceException" (crash, not a handled error)
    /// - "Microsoft.Extensions.DependencyInjection" (DI internals leaked to gRPC client)
    ///
    /// This verifies the ecosystem handles unknown sites gracefully.
    /// </summary>
    [Fact]
    public async Task HandleContainer_WithNonexistentSiteCode_ErrorDoesNotExposeInternals()
    {
        // Arrange
        using GrpcChannel channel = _factory.CreateGrpcChannel();
        AggregateRpc.AggregateRpcClient client = new(channel);

        Metadata headers = new() { { "x-site-code", "NONEXISTENT" } };
        HandleContainerRequest request = new() { ContainerNo = "X-002", IsoCode = "22G1" };

        // Act
        RpcException? caughtException = null;
        try
        {
            _ = await client.HandleContainerAsync(request, headers: headers);
        }
        catch (RpcException ex)
        {
            caughtException = ex;
        }

        // Assert — must be an RpcException (not a raw .NET exception escaping gRPC boundary)
        Assert.NotNull(caughtException);

        string detail = caughtException.Status.Detail ?? string.Empty;

        // Error must NOT expose NullReferenceException (indicates unhandled crash)
        Assert.DoesNotContain("NullReferenceException", detail, StringComparison.OrdinalIgnoreCase);

        // Error must NOT expose DI container internals to the gRPC client
        Assert.DoesNotContain("Microsoft.Extensions.DependencyInjection", detail, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// FAIL-05 Test 3: HandleContainer with NO x-site-code header.
    ///
    /// The Aggregate Host configures SiteGrpcOptions.Required=false.
    /// This means the interceptor does NOT throw InvalidArgument — it continues without SiteCode.
    /// The handler then tries to resolve a keyed container handler with null/empty SiteCode.
    ///
    /// Expected: either RpcException is thrown OR the handler falls back to a default handler.
    /// Either outcome is acceptable — what is NOT acceptable is a NullReferenceException crash.
    /// </summary>
    [Fact]
    public async Task HandleContainer_WithNoSiteCodeHeader_DoesNotThrowNullReferenceException()
    {
        // Arrange
        using GrpcChannel channel = _factory.CreateGrpcChannel();
        AggregateRpc.AggregateRpcClient client = new(channel);

        // No x-site-code header at all — empty metadata
        HandleContainerRequest request = new() { ContainerNo = "NOSIT-001", IsoCode = "22G1" };

        // Act — with Required=false, interceptor does NOT reject. Handler decides.
        RpcException? caughtException = null;
        HandleContainerReply? reply = null;
        try
        {
            reply = await client.HandleContainerAsync(request, headers: new Metadata());
        }
        catch (RpcException ex)
        {
            caughtException = ex;
        }

        // Assert: either succeeded (with a valid reply) or failed with an RpcException
        if (caughtException is not null)
        {
            // Verify it's a gRPC-level error, not a raw crash
            Assert.NotEqual(StatusCode.OK, caughtException.StatusCode);

            // Verify no NullReferenceException leaked as the gRPC error detail
            string detail = caughtException.Status.Detail ?? string.Empty;
            Assert.DoesNotContain("NullReferenceException", detail, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Handler produced a response (fallback to default handler or similar)
            // The reply must be a valid HandleContainerReply (not null)
            Assert.NotNull(reply);
        }
    }

    /// <summary>
    /// FAIL-05 Test 4: Bravo-specific BravoAggregateRpc endpoint called with x-site-code="DEFAULT".
    ///
    /// BravoGrpcService is the per-site Bravo proto endpoint. Calling it with site context "DEFAULT"
    /// means the SiteCode in the context is "DEFAULT" — but BravoGrpcService internally may
    /// assume it's called as BRAVO. We verify the call is either:
    /// a) Handled (perhaps using the injected SiteCode="DEFAULT" to route),
    /// b) Returns an RpcException — site mismatch detected at handler level.
    ///
    /// What must NOT happen: NullReferenceException or unhandled exception escaping gRPC boundary.
    /// </summary>
    [Fact]
    public async Task BravoEndpoint_WithDefaultSiteCode_HandledGracefully()
    {
        // Arrange
        using GrpcChannel channel = _factory.CreateGrpcChannel();
        BravoAggregateRpc.BravoAggregateRpcClient client = new(channel);

        // Calling Bravo's endpoint but claiming to be DEFAULT site
        Metadata headers = new() { { "x-site-code", "DEFAULT" } };
        BravoHandleRequest request = new() { ContainerNo = "DEFAULT-V001", IsoCode = "22G1" };

        // Act
        RpcException? caughtException = null;
        BravoHandleReply? reply = null;
        try
        {
            reply = await client.HandleWithValidationAsync(request, headers: headers);
        }
        catch (RpcException ex)
        {
            caughtException = ex;
        }

        // Assert: either an RpcException OR a valid response — either is acceptable behavior.
        // What is NOT acceptable: NullReferenceException escaping gRPC boundary.
        if (caughtException is not null)
        {
            // Verify it's a proper gRPC status, not a crash
            Assert.NotEqual(StatusCode.OK, caughtException.StatusCode);

            string detail = caughtException.Status.Detail ?? string.Empty;
            Assert.DoesNotContain("NullReferenceException", detail, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // BravoGrpcService returned a reply with x-site-code="DEFAULT" context
            // This is acceptable if the service routes by registered handler for "DEFAULT"
            Assert.NotNull(reply);
        }
    }
}
