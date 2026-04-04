using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.License;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Models.Common;
using System.Diagnostics;
using Grpc.Core;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Polly.CircuitBreaker;
using Polly.Wrap;

namespace Muonroi.Grpc.Grpc;

/// <summary>
/// Base class for gRPC client services with retries, timeouts, and telemetry.
/// </summary>
/// <param name="contextAccessor">Execution context accessor for metadata propagation.</param>
/// <param name="licenseState">Optional license state override.</param>
/// <param name="grpcConfigOptions">Optional gRPC configuration options.</param>
/// <param name="licenseGuard">Optional license guard for feature checks.</param>
public abstract class BaseGrpcService(
    ISystemExecutionContextAccessor contextAccessor,
    LicenseState? licenseState = null,
    IOptions<GrpcServicesConfig>? grpcConfigOptions = null,
    ILicenseGuard? licenseGuard = null)
{
    private readonly ISystemExecutionContextAccessor _contextAccessor = MGuard.NotNull(contextAccessor);
    private readonly LicenseState _licenseState = licenseState ?? LicenseState.CreateFree();
    private readonly GrpcClientDefaultsConfig _clientDefaults = grpcConfigOptions?.Value.ClientDefaults ?? new();

    /// <summary>
    /// Builds gRPC metadata from the current execution context.
    /// </summary>
    protected Metadata CreateMetadata()
    {
        Metadata metadata = [];
        ISystemExecutionContext context = _contextAccessor.Get();

        if (!string.IsNullOrEmpty(context.CorrelationId))
        {
            metadata.Add(CustomHeader.CorrelationId, context.CorrelationId);
        }

        if (!string.IsNullOrEmpty(context.ApiKey))
        {
            metadata.Add(CustomHeader.ApiKey, context.ApiKey);
        }

        string? tenantId = context.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            metadata.Add(CustomHeader.TenantId, tenantId);
        }

        return metadata;
    }

    /// <summary>
    /// Executes a gRPC call using default policies.
    /// </summary>
    protected async Task<MResponse> CallGrpcServiceAsync<MResponse>(Func<Metadata, Task<MResponse>> grpcCall)
    {
        return await CallGrpcServiceAsync("unknown", grpcCall, null);
    }

    /// <summary>
    /// Executes a gRPC call using resolved method policies.
    /// </summary>
    protected async Task<MResponse> CallGrpcServiceAsync<MResponse>(
        string methodName,
        Func<Metadata, Task<MResponse>> grpcCall,
        GrpcMethodPolicyConfig? policy)
    {
        _ = MGuard.NotNull(grpcCall);
        EnsureGrpcLicensed();

        Metadata metadata = CreateMetadata();
        GrpcMethodPolicyConfig resolvedPolicy = ResolvePolicy(policy);

        AsyncRetryPolicy<MResponse> retryPolicy = Policy<MResponse>
            .Handle<RpcException>(ex => ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
            .WaitAndRetryAsync(
                Math.Max(0, resolvedPolicy.RetryCount ?? _clientDefaults.RetryCount),
                retryAttempt =>
                    TimeSpan.FromSeconds(Math.Min(
                        Math.Pow(2, retryAttempt) * Math.Max(1, resolvedPolicy.InitialBackoffSeconds ?? _clientDefaults.InitialBackoffSeconds),
                        Math.Max(1, resolvedPolicy.MaxBackoffSeconds ?? _clientDefaults.MaxBackoffSeconds))));

        int timeoutSeconds = Math.Max(1, resolvedPolicy.TimeoutSeconds ?? _clientDefaults.TimeoutSeconds);
        AsyncTimeoutPolicy<MResponse> timeoutPolicy = global::Polly.Policy.TimeoutAsync<MResponse>(TimeSpan.FromSeconds(timeoutSeconds));

        AsyncCircuitBreakerPolicy<MResponse> circuitBreakerPolicy = Policy<MResponse>
            .Handle<RpcException>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        AsyncPolicyWrap<MResponse> policyWrap = global::Polly.Policy.WrapAsync(retryPolicy, timeoutPolicy, circuitBreakerPolicy);
        Stopwatch sw = Stopwatch.StartNew();
        string? tenantId = ResolveTenantId();
        using Activity? activity = GrpcRuntimeTelemetry.ActivitySource.StartActivity(methodName, ActivityKind.Client);
        activity?.SetTag("grpc.method", methodName);
        activity?.SetTag("grpc.call_type", "unary");
        activity?.SetTag("tenant.id", tenantId ?? string.Empty);

        StatusCode statusCode = StatusCode.OK;
        try
        {
            return await policyWrap.ExecuteAsync(() => grpcCall(metadata));
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
            GrpcRuntimeTelemetry.TrackRequest(methodName, "unary", statusCode.ToString(), tenantId, sw.Elapsed);
        }
    }

    private static GrpcMethodPolicyConfig ResolvePolicy(GrpcMethodPolicyConfig? policy)
    {
        return policy ?? new GrpcMethodPolicyConfig();
    }

    private void EnsureGrpcLicensed()
    {
        if (licenseGuard is not null)
        {
            licenseGuard.EnsureFeature(FreeTierFeatures.Premium.Grpc);
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
