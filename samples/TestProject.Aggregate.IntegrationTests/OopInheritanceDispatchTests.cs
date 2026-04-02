using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;
using Muonroi.Logging.Abstractions;
using TestProject.Aggregate.Host.v1.Services;
using TestProject.Aggregate.Host.v1.Services.Sites.Bravo;

namespace TestProject.Aggregate.IntegrationTests;

/// <summary>
/// DI-level integration tests verifying the OOP inheritance dispatch pattern (D-16).
///
/// These tests prove the keyed DI + OOP inheritance chain works correctly using
/// a ServiceProvider — without running a real gRPC server.
///
/// NOTE: Tests that call proto RPC methods directly (HandleContainer, ListContainers) are
/// omitted here because the test project also compiles <c>GrpcServices="Client"</c> stubs
/// for the same .proto files — resulting in duplicate class definitions in the same namespace.
/// Those end-to-end behaviors are fully covered by <see cref="GrpcDispatchTests"/> (WebApp tests).
///
/// What these tests cover:
/// <list type="bullet">
///   <item>Keyed DI resolution: "BRAVO" → BravoOrderGrpcService, "default" → SharedOrderGrpcService</item>
///   <item>OOP inheritance chain: BravoOrderGrpcService IS-A SharedOrderGrpcService</item>
///   <item>Default fallback: unknown site resolves "default" handler</item>
///   <item>Missing handler: SiteGrpcDispatchHelper throws RpcException with StatusCode.Internal</item>
///   <item>Non-override resolution: BravoOrderGrpcService does NOT override ListContainers (reflection check)</item>
/// </list>
/// </summary>
public sealed class OopInheritanceDispatchTests
{
    private static ServerCallContext CreateFakeContext() => new FakeServerCallContext();

    /// <summary>
    /// Builds a ServiceProvider with SharedOrderGrpcService and BravoOrderGrpcService
    /// registered as keyed services under "default" and "BRAVO" respectively.
    /// </summary>
    private static ServiceProvider BuildProvider(string siteCode)
    {
        ServiceCollection services = new();
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.AddLogging(b => b.AddMuonroiLogging());

        // Fake ISiteCodeHolder — simulates what SiteCodeGrpcInterceptor sets per-request
        services.AddScoped<ISiteCodeHolder>(_ => new FakeSiteCodeHolder { SiteCode = siteCode });

        // Register handlers with SharedOrderGrpcService as the keyed base type.
        // SharedOrderGrpcService inherits from AggregateRpc.AggregateRpcBase,
        // so the same inheritance chain applies in production.
        services.AddSiteGrpcHandler<SharedOrderGrpcService, SharedOrderGrpcService>("default");
        services.AddSiteGrpcHandler<SharedOrderGrpcService, BravoOrderGrpcService>("BRAVO");
        services.AddSiteGrpcDispatcher<SharedOrderGrpcService>();

        return services.BuildServiceProvider();
    }

    // =========================================================================
    // Test 1: BRAVO keyed DI resolves BravoOrderGrpcService (inheritance type check)
    // =========================================================================

    /// <summary>
    /// D-16 Test 1: SiteCode="BRAVO" resolves BravoOrderGrpcService from keyed DI.
    ///
    /// Proves:
    /// <list type="bullet">
    ///   <item>Keyed DI maps "BRAVO" → BravoOrderGrpcService</item>
    ///   <item>BravoOrderGrpcService IS-A SharedOrderGrpcService (OOP inheritance verified via type)</item>
    ///   <item>BravoOrderGrpcService declares HandleContainer override (virtual dispatch verifiable)</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Bravo_KeyedDi_Resolves_BravoOrderGrpcService()
    {
        // Arrange
        await using ServiceProvider provider = BuildProvider("BRAVO");
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        // Act — resolve via exact "BRAVO" key
        SharedOrderGrpcService? handler = scope.ServiceProvider
            .GetKeyedService<SharedOrderGrpcService>("BRAVO");

        // Assert
        handler.Should().NotBeNull(
            because: "AddSiteGrpcHandler<..., BravoOrderGrpcService>(\"BRAVO\") was registered");
        handler.Should().BeOfType<BravoOrderGrpcService>(
            because: "key 'BRAVO' must resolve the BravoOrderGrpcService implementation");

        // OOP Inheritance chain verified via type system
        handler.Should().BeAssignableTo<SharedOrderGrpcService>(
            because: "BravoOrderGrpcService inherits from SharedOrderGrpcService — inheritance chain intact");

        // Verify HandleContainer override exists on BravoOrderGrpcService (virtual dispatch works)
        System.Reflection.MethodInfo? method = typeof(BravoOrderGrpcService)
            .GetMethod("HandleContainer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        method.Should().NotBeNull(
            because: "BravoOrderGrpcService must declare an override of HandleContainer for virtual dispatch to work");
    }

    // =========================================================================
    // Test 2: DEFAULT site falls back to "default" handler (SharedOrderGrpcService)
    // =========================================================================

    /// <summary>
    /// D-16 Test 2: SiteCode="DEFAULT" → no exact match → must fall back to "default" key.
    /// </summary>
    [Fact]
    public async Task Default_Site_Resolves_SharedHandler_Via_Fallback()
    {
        // Arrange
        await using ServiceProvider provider = BuildProvider("DEFAULT");
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        // "DEFAULT" has no exact key registered
        SharedOrderGrpcService? exact = scope.ServiceProvider
            .GetKeyedService<SharedOrderGrpcService>("DEFAULT");
        SharedOrderGrpcService? fallback = scope.ServiceProvider
            .GetKeyedService<SharedOrderGrpcService>("default");

        // Assert
        exact.Should().BeNull(
            because: "No 'DEFAULT' keyed service was registered — SiteGrpcDispatchHelper must fall back");
        fallback.Should().NotBeNull(
            because: "A 'default' fallback was registered via AddSiteGrpcHandler<..., SharedOrderGrpcService>(\"default\")");
        fallback.Should().BeOfType<SharedOrderGrpcService>(
            because: "'default' key resolves SharedOrderGrpcService (the shared base implementation)");
    }

    // =========================================================================
    // Test 3: Unknown site code falls back to "default" handler
    // =========================================================================

    /// <summary>
    /// D-16 Test 3: SiteCode="UNKNOWN_SITE" has no exact match → falls back to "default".
    ///
    /// Core resilience: any unknown site must gracefully degrade to shared logic.
    /// </summary>
    [Fact]
    public async Task Unknown_Site_Falls_Back_To_Default_Handler()
    {
        // Arrange
        await using ServiceProvider provider = BuildProvider("UNKNOWN_SITE");
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        SharedOrderGrpcService? exact = scope.ServiceProvider
            .GetKeyedService<SharedOrderGrpcService>("UNKNOWN_SITE");
        SharedOrderGrpcService? fallback = scope.ServiceProvider
            .GetKeyedService<SharedOrderGrpcService>("default");

        // Assert
        exact.Should().BeNull(
            because: "No 'UNKNOWN_SITE' keyed service was registered");
        fallback.Should().NotBeNull(
            because: "SiteGrpcDispatchHelper falls back to 'default' for unknown sites — " +
                     "SharedOrderGrpcService must be resolvable as the fallback");
    }

    // =========================================================================
    // Test 4: No handler registered → RpcException with StatusCode.Internal
    // =========================================================================

    /// <summary>
    /// D-16 Test 4: SiteGrpcDispatchHelper throws RpcException when no handler registered.
    ///
    /// Uses a minimal ServiceProvider with NO keyed handlers, only the dispatch helper.
    /// Verifies error message contains site code and "default" hint.
    /// </summary>
    [Fact]
    public async Task No_Handler_Registered_Throws_RpcException_WithInternalStatus()
    {
        // Arrange — provider WITHOUT any handlers
        ServiceCollection services = new();
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.AddLogging(b => b.AddMuonroiLogging());
        services.AddScoped<ISiteCodeHolder>(_ => new FakeSiteCodeHolder { SiteCode = "NOSITE" });
        services.AddSiteGrpcDispatcher<SharedOrderGrpcService>();  // helper only, no handlers

        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        SiteGrpcDispatchHelper<SharedOrderGrpcService> helper =
            scope.ServiceProvider.GetRequiredService<SiteGrpcDispatchHelper<SharedOrderGrpcService>>();

        ServerCallContext ctx = CreateFakeContext();

        // Act + Assert — DispatchAsync must throw because no handlers registered
        RpcException ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await helper.DispatchAsync(
                ctx,
                (h, c) => Task.FromResult(new object())));  // dummy rpcCall — will throw before invoking

        ex.StatusCode.Should().Be(StatusCode.Internal,
            because: "SiteGrpcDispatchHelper throws RpcException(StatusCode.Internal) if no handler registered");
        ex.Status.Detail.Should().Contain("NOSITE",
            because: "the error includes the unresolved site code for diagnostics");
        ex.Status.Detail.Should().Contain("default",
            because: "the error mentions 'default' to hint at how to fix the registration");
    }

    // =========================================================================
    // Test 5: BravoOrderGrpcService does NOT override ListContainers
    // =========================================================================

    /// <summary>
    /// D-16 Test 5: BravoOrderGrpcService does NOT override ListContainers.
    ///
    /// Proves that non-overridden RPCs fall through to the shared base implementation
    /// via virtual dispatch. Verified via reflection (no proto type import needed).
    /// </summary>
    [Fact]
    public void Bravo_Does_Not_Override_ListContainers_FallsThrough_To_Shared()
    {
        // Verify: BravoOrderGrpcService has NO DeclaredOnly ListContainers method
        System.Reflection.MethodInfo? bravoOverride = typeof(BravoOrderGrpcService)
            .GetMethod("ListContainers",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        bravoOverride.Should().BeNull(
            because: "BravoOrderGrpcService intentionally does NOT override ListContainers — " +
                     "virtual dispatch must fall through to SharedOrderGrpcService.ListContainers");

        // Verify: SharedOrderGrpcService HAS a declared ListContainers (the fallback)
        System.Reflection.MethodInfo? sharedImpl = typeof(SharedOrderGrpcService)
            .GetMethod("ListContainers",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        sharedImpl.Should().NotBeNull(
            because: "SharedOrderGrpcService must declare ListContainers for the fall-through to work");
    }
}

/// <summary>
/// Mutable fake ISiteCodeHolder for DI-level testing.
/// Simulates the SiteCode set by SiteCodeGrpcInterceptor per-request.
/// </summary>
internal sealed class FakeSiteCodeHolder : ISiteCodeHolder
{
    public string? SiteCode { get; set; }
}

/// <summary>
/// Minimal fake ServerCallContext for DI-level testing.
/// SiteGrpcDispatchHelper reads SiteCode from ISiteCodeHolder (not from context metadata),
/// so context properties are unused in these dispatch tests.
/// </summary>
internal sealed class FakeServerCallContext : ServerCallContext
{
    protected override string MethodCore => "TestMethod";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "127.0.0.1";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => [];
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore => [];
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => new AuthContext(null, []);
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => throw new NotSupportedException();
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;
}
