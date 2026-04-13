namespace Muonroi.Grpc.Tests;

using System.Text;

public class GrpcClientAuthInterceptorTests
{
    private static readonly Marshaller<string> StringMarshaller = new(
        static value => Encoding.UTF8.GetBytes(value),
        static data => Encoding.UTF8.GetString(data));

    private static readonly Method<string, string> UnaryMethod = new(
        MethodType.Unary,
        "muonroi.tests.Auth",
        "Echo",
        StringMarshaller,
        StringMarshaller);

    [Fact]
    public async Task AsyncUnaryCall_Adds_Authorization_And_Tenant_Headers_When_Enabled()
    {
        SystemExecutionContextAccessor accessor = CreateAccessor(accessToken: "token-123", tenantId: "tenant-a", correlationId: "corr-a");
        GrpcClientAuthInterceptor interceptor = new(accessor, new GrpcClientAuthForwardingOptions
        {
            ForwardAuthToken = true,
            ForwardTenantId = true
        });

        Metadata? capturedHeaders = null;

        AsyncUnaryCall<string> call = interceptor.AsyncUnaryCall(
            "request",
            CreateContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateUnaryCall("ok");
            });

        string response = await call.ResponseAsync;

        response.Should().Be("ok");
        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.GetValue("authorization").Should().Be("Bearer token-123");
        capturedHeaders.GetValue(CustomHeader.TenantId).Should().Be("tenant-a");
        capturedHeaders.GetValue(CustomHeader.CorrelationId).Should().Be("corr-a");
    }

    [Fact]
    public async Task AsyncUnaryCall_Does_Not_Add_Authorization_Header_When_Disabled()
    {
        SystemExecutionContextAccessor accessor = CreateAccessor(accessToken: "token-123", tenantId: "tenant-a", correlationId: "corr-a");
        GrpcClientAuthInterceptor interceptor = new(accessor, new GrpcClientAuthForwardingOptions
        {
            ForwardAuthToken = false,
            ForwardTenantId = true
        });

        Metadata? capturedHeaders = null;

        AsyncUnaryCall<string> call = interceptor.AsyncUnaryCall(
            "request",
            CreateContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateUnaryCall("ok");
            });

        _ = await call.ResponseAsync;

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.GetValue("authorization").Should().BeNull();
        capturedHeaders.GetValue(CustomHeader.TenantId).Should().Be("tenant-a");
    }

    [Fact]
    public async Task AsyncUnaryCall_Does_Not_Add_Tenant_Header_When_Disabled()
    {
        SystemExecutionContextAccessor accessor = CreateAccessor(accessToken: "token-123", tenantId: "tenant-a", correlationId: "corr-a");
        GrpcClientAuthInterceptor interceptor = new(accessor, new GrpcClientAuthForwardingOptions
        {
            ForwardAuthToken = true,
            ForwardTenantId = false
        });

        Metadata? capturedHeaders = null;

        AsyncUnaryCall<string> call = interceptor.AsyncUnaryCall(
            "request",
            CreateContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateUnaryCall("ok");
            });

        _ = await call.ResponseAsync;

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.GetValue(CustomHeader.TenantId).Should().BeNull();
        capturedHeaders.GetValue(CustomHeader.CorrelationId).Should().Be("corr-a");
    }

    [Fact]
    public void BlockingUnaryCall_Always_Forwards_CorrelationId_When_Present()
    {
        SystemExecutionContextAccessor accessor = CreateAccessor(accessToken: null, tenantId: null, correlationId: "corr-blocking");
        GrpcClientAuthInterceptor interceptor = new(accessor, new GrpcClientAuthForwardingOptions());

        Metadata? capturedHeaders = null;

        string response = interceptor.BlockingUnaryCall(
            "request",
            CreateContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return "ok";
            });

        response.Should().Be("ok");
        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.GetValue(CustomHeader.CorrelationId).Should().Be("corr-blocking");
    }

    [Fact]
    public async Task AsyncUnaryCall_Does_Not_Crash_When_AccessToken_Is_Missing()
    {
        SystemExecutionContextAccessor accessor = CreateAccessor(accessToken: null, tenantId: "tenant-a", correlationId: "corr-a");
        GrpcClientAuthInterceptor interceptor = new(accessor, new GrpcClientAuthForwardingOptions
        {
            ForwardAuthToken = true,
            ForwardTenantId = true
        });

        Metadata? capturedHeaders = null;

        AsyncUnaryCall<string> call = interceptor.AsyncUnaryCall(
            "request",
            CreateContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateUnaryCall("ok");
            });

        string response = await call.ResponseAsync;

        response.Should().Be("ok");
        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.GetValue("authorization").Should().BeNull();
        capturedHeaders.GetValue(CustomHeader.TenantId).Should().Be("tenant-a");
    }

    [Fact]
    public async Task AsyncUnaryCall_Does_Not_Duplicate_Bearer_Prefix()
    {
        SystemExecutionContextAccessor accessor = CreateAccessor(accessToken: "Bearer token-123", tenantId: "tenant-a", correlationId: "corr-a");
        GrpcClientAuthInterceptor interceptor = new(accessor, new GrpcClientAuthForwardingOptions
        {
            ForwardAuthToken = true,
            ForwardTenantId = true
        });

        Metadata? capturedHeaders = null;

        AsyncUnaryCall<string> call = interceptor.AsyncUnaryCall(
            "request",
            CreateContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateUnaryCall("ok");
            });

        _ = await call.ResponseAsync;

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.GetValue("authorization").Should().Be("Bearer token-123");
    }

    private static SystemExecutionContextAccessor CreateAccessor(string? accessToken, string? tenantId, string correlationId)
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext(
            tenantId: tenantId,
            userId: "user-1",
            username: "tester",
            correlationId: correlationId,
            accessToken: accessToken,
            apiKey: "api-key",
            isAuthenticated: true,
            permissions: [],
            sourceType: "test"));
        return accessor;
    }

    private static ClientInterceptorContext<string, string> CreateContext(Metadata? headers = null)
    {
        return new ClientInterceptorContext<string, string>(
            UnaryMethod,
            "localhost",
            new CallOptions(headers: headers));
    }

    private static AsyncUnaryCall<string> CreateUnaryCall(string response)
    {
        return new AsyncUnaryCall<string>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            static () => new Status(StatusCode.OK, string.Empty),
            static () => new Metadata(),
            static () => { });
    }
}
