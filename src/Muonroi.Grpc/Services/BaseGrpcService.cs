using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.License;
using Muonroi.Grpc.Grpc;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Observability.OpenTelemetry;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Grpc.Core;
namespace Muonroi.Grpc.Services;

/// <summary>
/// Base class for gRPC client services with retry and timeout policies.
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
        _ = grpcCall ?? throw new NullReferenceException(nameof(grpcCall));
        EnsureGrpcLicensed();

        Metadata metadata = CreateMetadata();
        GrpcMethodPolicyConfig resolvedPolicy = ResolvePolicy(policy);

        // Polly v8 Resilience Pipeline (D-04)
        var pipeline = new ResiliencePipelineBuilder<MResponse>()
            .AddRetry(new RetryStrategyOptions<MResponse>
            {
                // D-05: Handle transient errors
                ShouldHandle = new PredicateBuilder<MResponse>()
                    .Handle<MTransientException>()
                    .Handle<RpcException>(ex => ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
                    .Handle<HttpRequestException>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxRetryAttempts = Math.Max(0, resolvedPolicy.RetryCount ?? _clientDefaults.RetryCount),
                Delay = TimeSpan.FromSeconds(Math.Max(1, resolvedPolicy.InitialBackoffSeconds ?? _clientDefaults.InitialBackoffSeconds)),
                OnRetry = args =>
                {
                    // Track retry attempt (D-03)
                    MuonroiMetrics.RetryAttemptCount.Add(1, 
                        new KeyValuePair<string, object?>("grpc.method", methodName),
                        new KeyValuePair<string, object?>("exception.type", args.Outcome.Exception?.GetType().Name));
                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<MResponse>
            {
                ShouldHandle = new PredicateBuilder<MResponse>().Handle<Exception>(),
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .AddTimeout(TimeSpan.FromSeconds(Math.Max(1, resolvedPolicy.TimeoutSeconds ?? _clientDefaults.TimeoutSeconds)))
            .Build();

        Stopwatch sw = Stopwatch.StartNew();
        string? tenantId = ResolveTenantId();
        using Activity? activity = GrpcRuntimeTelemetry.ActivitySource.StartActivity(methodName, ActivityKind.Client);
        activity?.SetTag("grpc.method", methodName);
        activity?.SetTag("grpc.call_type", "unary");
        activity?.SetTag("tenant.id", tenantId ?? string.Empty);

        StatusCode statusCode = StatusCode.OK;
        try
        {
            return await pipeline.ExecuteAsync(async ct => await grpcCall(metadata));
        }
        catch (RpcException rpcEx)
        {
            statusCode = rpcEx.StatusCode;
            activity?.SetStatus(ActivityStatusCode.Error, rpcEx.Status.Detail);
            MuonroiTraceProcessor.TagException(activity, rpcEx); // D-02
            throw;
        }
        catch (Exception ex)
        {
            statusCode = StatusCode.Unknown;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            MuonroiTraceProcessor.TagException(activity, ex); // D-02
            
            // Track exception count (D-03)
            if (ex is MException mex)
            {
                MuonroiMetrics.ExceptionCount.Add(1, 
                    new KeyValuePair<string, object?>("category", mex.Category.ToString()),
                    new KeyValuePair<string, object?>("error_code", mex.ErrorCode));
            }
            
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
