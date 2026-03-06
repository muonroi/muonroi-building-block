using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Governance.License;

namespace Muonroi.Grpc.Grpc;

public sealed class GrpcClientTelemetryInterceptor(
    ISystemExecutionContextAccessor contextAccessor,
    LicenseState? licenseState = null,
    ILicenseGuard? licenseGuard = null)
    : Interceptor
{
    private readonly ISystemExecutionContextAccessor _contextAccessor =
        contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
    private readonly LicenseState _licenseState = licenseState ?? LicenseState.CreateFree();
    private readonly ILicenseGuard? _licenseGuard = licenseGuard;

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        EnsureGrpcLicensed();
        Metadata metadata = AppendMetadata(context.Options.Headers);
        ClientInterceptorContext<TRequest, TResponse> updatedContext = new(
            context.Method,
            context.Host,
            context.Options.WithHeaders(metadata));

        Stopwatch sw = Stopwatch.StartNew();
        string? tenantId = ResolveTenantId();
        using Activity? activity = GrpcRuntimeTelemetry.ActivitySource.StartActivity(context.Method.FullName, ActivityKind.Client);
        activity?.SetTag("grpc.method", context.Method.FullName);
        activity?.SetTag("grpc.call_type", "unary");
        activity?.SetTag("tenant.id", tenantId ?? string.Empty);

        AsyncUnaryCall<TResponse> call = continuation(request, updatedContext);
        Task<TResponse> responseTask = HandleResponseAsync(call.ResponseAsync, context.Method.FullName, "unary", tenantId, sw, activity);

        return new AsyncUnaryCall<TResponse>(
            responseTask,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private Metadata AppendMetadata(Metadata? headers)
    {
        Metadata metadata = headers ?? [];
        ISystemExecutionContext current = _contextAccessor.Get();
        if (!string.IsNullOrWhiteSpace(current.CorrelationId) &&
            metadata.GetValue(CustomHeader.CorrelationId) is null)
        {
            metadata.Add(CustomHeader.CorrelationId, current.CorrelationId);
        }

        if (!string.IsNullOrWhiteSpace(current.ApiKey) &&
            metadata.GetValue(CustomHeader.ApiKey) is null)
        {
            metadata.Add(CustomHeader.ApiKey, current.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(current.TenantId) &&
            metadata.GetValue(CustomHeader.TenantId) is null)
        {
            metadata.Add(CustomHeader.TenantId, current.TenantId);
        }

        return metadata;
    }

    private static async Task<TResponse> HandleResponseAsync<TResponse>(
        Task<TResponse> task,
        string method,
        string callType,
        string? tenantId,
        Stopwatch sw,
        Activity? activity)
    {
        StatusCode statusCode = StatusCode.OK;
        try
        {
            TResponse? response = await task.ConfigureAwait(false);
            return response;
        }
        catch (RpcException rpcEx)
        {
            statusCode = rpcEx.StatusCode;
            activity?.SetStatus(ActivityStatusCode.Error, rpcEx.Status.Detail);
            throw;
        }
        catch (Exception ex)
        {
            statusCode = StatusCode.Unknown;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            GrpcRuntimeTelemetry.TrackRequest(
                method,
                callType,
                statusCode.ToString(),
                tenantId,
                sw.Elapsed);
        }
    }

    private void EnsureGrpcLicensed()
    {
        if (_licenseGuard is not null)
        {
            _licenseGuard.EnsureFeature(FreeTierFeatures.Premium.Grpc);
            return;
        }

        if (!_licenseState.HasFeature(FreeTierFeatures.Premium.Grpc))
        {
            throw new RpcException(new global::Grpc.Core.Status(
                StatusCode.PermissionDenied,
                "[LICENSE] Feature 'grpc' is not available under your current license."));
        }
    }

    private string? ResolveTenantId()
    {
        return _contextAccessor.Get().TenantId;
    }
}
