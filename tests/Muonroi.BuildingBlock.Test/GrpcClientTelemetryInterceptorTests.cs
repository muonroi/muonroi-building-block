using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class GrpcClientTelemetryInterceptorTests
{
    private static readonly LicenseState GrpcLicensed = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.Grpc]
    };

    private static readonly Marshaller<string> StringMarshaller = Marshallers.Create(
        value => Encoding.UTF8.GetBytes(value ?? string.Empty),
        payload => Encoding.UTF8.GetString(payload));

    private sealed class DenyGrpcGuard : ILicenseGuard
    {
        private static readonly LicenseState State = LicenseState.CreateFree();
        public LicenseState Current => State;
        public LicenseTier Tier => State.Tier;
        public bool IsFreeMode => true;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.Grpc, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
                throw new InvalidOperationException("grpc feature not licensed");
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken() => "test";

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return decryptor("k", encryptedData);
        }
    }

    private static AsyncUnaryCall<string> CreateUnaryCall(Task<string>? responseTask = null)
    {
        return new AsyncUnaryCall<string>(
            responseTask ?? Task.FromResult("ok"),
            Task.FromResult(new Metadata()),
            () => new Grpc.Core.Status(StatusCode.OK, string.Empty),
            () => [],
            () => { });
    }

    private static Grpc.Core.Interceptors.ClientInterceptorContext<string, string> CreateClientContext(Metadata? headers = null)
    {
        Method<string, string> method = new(
            MethodType.Unary,
            "test.Service",
            "test.Method",
            StringMarshaller,
            StringMarshaller);

        CallOptions options = headers is null ? new CallOptions() : new CallOptions(headers);
        return new Grpc.Core.Interceptors.ClientInterceptorContext<string, string>(method, "localhost", options);
    }

    [Fact]
    public async Task AsyncUnaryCall_Appends_Metadata_FromAuthContext()
    {
        MAuthenticateInfoContext authContext = new(false)
        {
            CorrelationId = "corr-1",
            ApiKey = "api-1",
            TenantId = "tenant-auth"
        };
        TenantContext.CurrentTenantId = null;
        GrpcClientTelemetryInterceptor interceptor = new(authContext, GrpcLicensed);

        Metadata? capturedHeaders = null;
        Grpc.Core.Interceptors.ClientInterceptorContext<string, string> context = CreateClientContext();

        AsyncUnaryCall<string> call = interceptor.AsyncUnaryCall(
            "req",
            context,
            (_, ctx) =>
            {
                capturedHeaders = ctx.Options.Headers;
                return CreateUnaryCall();
            });

        string response = await call.ResponseAsync;

        Assert.Equal("ok", response);
        Assert.NotNull(capturedHeaders);
        Assert.Equal("corr-1", capturedHeaders!.GetValue(CustomHeader.CorrelationId));
        Assert.Equal("api-1", capturedHeaders.GetValue(CustomHeader.ApiKey));
        Assert.Equal("tenant-auth", capturedHeaders.GetValue(CustomHeader.TenantId));
    }

    [Fact]
    public async Task AsyncUnaryCall_RuntimeTenant_ShouldOverride_AuthTenant()
    {
        MAuthenticateInfoContext authContext = new(false)
        {
            CorrelationId = "corr-1",
            ApiKey = "api-1",
            TenantId = "tenant-auth"
        };
        TenantContext.CurrentTenantId = "tenant-runtime";
        GrpcClientTelemetryInterceptor interceptor = new(authContext, GrpcLicensed);

        Metadata? capturedHeaders = null;
        Grpc.Core.Interceptors.ClientInterceptorContext<string, string> context = CreateClientContext();

        AsyncUnaryCall<string> call = interceptor.AsyncUnaryCall(
            "req",
            context,
            (_, ctx) =>
            {
                capturedHeaders = ctx.Options.Headers;
                return CreateUnaryCall();
            });

        _ = await call.ResponseAsync;

        Assert.NotNull(capturedHeaders);
        Assert.Equal("tenant-runtime", capturedHeaders!.GetValue(CustomHeader.TenantId));
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public void AsyncUnaryCall_FreeMode_ShouldThrowRpcException()
    {
        MAuthenticateInfoContext authContext = new(false);
        GrpcClientTelemetryInterceptor interceptor = new(authContext, LicenseState.CreateFree());
        Grpc.Core.Interceptors.ClientInterceptorContext<string, string> context = CreateClientContext();

        RpcException ex = Assert.Throws<RpcException>(() =>
            interceptor.AsyncUnaryCall(
                "req",
                context,
                (_, _) => CreateUnaryCall()));

        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Fact]
    public void AsyncUnaryCall_LicenseGuardDenies_ShouldThrow()
    {
        MAuthenticateInfoContext authContext = new(false);
        GrpcClientTelemetryInterceptor interceptor = new(authContext, GrpcLicensed, new DenyGrpcGuard());
        Grpc.Core.Interceptors.ClientInterceptorContext<string, string> context = CreateClientContext();

        MInternalException ex = Assert.Throws<MInternalException>(() =>
            interceptor.AsyncUnaryCall(
                "req",
                context,
                (_, _) => CreateUnaryCall()));

        Assert.Contains("grpc feature not licensed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
