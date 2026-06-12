using Microsoft.AspNetCore.Http;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Grpc.Grpc;

/// <summary>
/// Server-side gRPC interceptor with tenancy, licensing, and telemetry handling.
/// </summary>
public class GrpcServerInterceptor(
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy,
    MTokenInfo tokenInfo,
    IMLog<GrpcServerInterceptor>? logger = null,
    LicenseState? licenseState = null,
    IOptions<GrpcServicesConfig>? grpcConfigOptions = null,
    GrpcRateLimiter? rateLimiter = null,
    IOptions<Tenancy.Core.Legacy.MultiTenantConfigs>? multiTenantOptions = null,
    ILicenseGuard? licenseGuard = null,
    ILogScopeFactory? logScopeFactory = null)
    : Interceptor
{
    private readonly ISystemExecutionContextAccessor _executionContextAccessor = executionContextAccessor;
    private readonly ITenantContextPolicy _tenantContextPolicy = tenantContextPolicy;
    private readonly bool _multiTenantEnabled = tokenInfo.MultiTenantEnabled;
    private readonly LicenseState _licenseState = licenseState ?? LicenseState.CreateFree();
    private readonly GrpcServerConfig _serverConfig = grpcConfigOptions?.Value.Server ?? new GrpcServerConfig();
    private readonly GrpcRateLimiter _rateLimiter = rateLimiter ?? new GrpcRateLimiter();
    private readonly bool _requireTenantClaimForAuthenticatedUser =
        multiTenantOptions?.Value.RequireTenantClaimForAuthenticatedUser ?? true;
    private readonly ILicenseGuard? _licenseGuard = licenseGuard;

    /// <inheritdoc/>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        _ = MGuard.NotNull(context);
        EnsureGrpcLicensed();

        SystemExecutionContext executionContext = NormalizeExecutionContext(context, allowCorrelationTrailer: true);
        using SystemExecutionContextScope scope = new(_executionContextAccessor, executionContext);
        using ContextMirrorScope contextMirror = ContextMirrorScope.Apply(executionContext, logScopeFactory);
        using Activity? activity = StartActivity(context.Method, "unary", executionContext.TenantId);

        logger?.Info("gRPC call {Method} started CorrelationId={CorrelationId}",
            context.Method, executionContext.CorrelationId);

        Stopwatch sw = Stopwatch.StartNew();
        StatusCode statusCode = StatusCode.OK;

        try
        {
            TResponse response = await continuation(request, context);
            logger?.Info("gRPC call {Method} completed CorrelationId={CorrelationId}",
                context.Method, executionContext.CorrelationId);
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
            GrpcRuntimeTelemetry.TrackRequest(context.Method, "unary", statusCode.ToString(), executionContext.TenantId, sw.Elapsed);
        }
    }

    /// <inheritdoc/>
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        _ = MGuard.NotNull(context);
        EnsureGrpcLicensed();

        SystemExecutionContext executionContext = NormalizeExecutionContext(context, allowCorrelationTrailer: false);
        using SystemExecutionContextScope scope = new(_executionContextAccessor, executionContext);
        using ContextMirrorScope contextMirror = ContextMirrorScope.Apply(executionContext, logScopeFactory);
        using Activity? activity = StartActivity(context.Method, "client_streaming", executionContext.TenantId);

        Stopwatch sw = Stopwatch.StartNew();
        StatusCode statusCode = StatusCode.OK;

        try
        {
            return await base.ClientStreamingServerHandler(requestStream, context, continuation);
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
            GrpcRuntimeTelemetry.TrackRequest(context.Method, "client_streaming", statusCode.ToString(), executionContext.TenantId, sw.Elapsed);
        }
    }

    /// <inheritdoc/>
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        _ = MGuard.NotNull(context);
        EnsureGrpcLicensed();

        SystemExecutionContext executionContext = NormalizeExecutionContext(context, allowCorrelationTrailer: false);
        using SystemExecutionContextScope scope = new(_executionContextAccessor, executionContext);
        using ContextMirrorScope contextMirror = ContextMirrorScope.Apply(executionContext, logScopeFactory);
        using Activity? activity = StartActivity(context.Method, "server_streaming", executionContext.TenantId);

        Stopwatch sw = Stopwatch.StartNew();
        StatusCode statusCode = StatusCode.OK;
        try
        {
            await base.ServerStreamingServerHandler(request, responseStream, context, continuation);
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
            GrpcRuntimeTelemetry.TrackRequest(context.Method, "server_streaming", statusCode.ToString(), executionContext.TenantId, sw.Elapsed);
        }
    }

    /// <inheritdoc/>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        _ = MGuard.NotNull(context);
        EnsureGrpcLicensed();

        SystemExecutionContext executionContext = NormalizeExecutionContext(context, allowCorrelationTrailer: false);
        using SystemExecutionContextScope scope = new(_executionContextAccessor, executionContext);
        using ContextMirrorScope contextMirror = ContextMirrorScope.Apply(executionContext, logScopeFactory);
        using Activity? activity = StartActivity(context.Method, "duplex_streaming", executionContext.TenantId);

        Stopwatch sw = Stopwatch.StartNew();
        StatusCode statusCode = StatusCode.OK;
        try
        {
            await base.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation);
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
            GrpcRuntimeTelemetry.TrackRequest(context.Method, "duplex_streaming", statusCode.ToString(), executionContext.TenantId, sw.Elapsed);
        }
    }

    private SystemExecutionContext NormalizeExecutionContext(ServerCallContext context, bool allowCorrelationTrailer)
    {
        string correlationId = context.RequestHeaders.GetValue(CustomHeader.CorrelationId) ?? Guid.NewGuid().ToString("N");
        if (allowCorrelationTrailer)
        {
            context.ResponseTrailers.Add(CustomHeader.CorrelationId, correlationId);
        }

        string? tenantId = context.RequestHeaders.GetValue(CustomHeader.TenantId)?.Trim();
        ValidateTenantContext(context, tenantId);

        HttpContext? httpContext = TryGetHttpContext(context);
        ClaimsPrincipal? user = httpContext?.User;
        string? userId = context.RequestHeaders.GetValue(ClaimConstants.UserIdentifier)?.Trim()
                         ?? user?.FindFirst(ClaimConstants.UserIdentifier)?.Value
                         ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string? username = context.RequestHeaders.GetValue(ClaimConstants.Username)?.Trim()
                           ?? user?.FindFirst(ClaimConstants.Username)?.Value
                           ?? user?.Identity?.Name;
        string? accessToken = context.RequestHeaders.GetValue(ClaimConstants.AccessToken)?.Trim();
        string? apiKey = context.RequestHeaders.GetValue(CustomHeader.ApiKey)?.Trim();

        List<string> permissions = user?.Claims
                .Where(x => x.Type is ClaimConstants.Permission or ClaimTypes.Role)
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            ?? [];

        SystemExecutionContext raw = new(
            tenantId: tenantId,
            userId: userId,
            username: username,
            correlationId: correlationId,
            accessToken: accessToken,
            apiKey: apiKey,
            isAuthenticated: user?.Identity?.IsAuthenticated == true || !string.IsNullOrWhiteSpace(accessToken),
            permissions: permissions,
            sourceType: "grpc");

        SystemExecutionContext resolved = (SystemExecutionContext)_tenantContextPolicy.ResolveAndValidate(raw);
        EnforceRateLimit(resolved.TenantId, resolved.ApiKey);
        ValidateMutualTls(context);
        return resolved;
    }

    private void ValidateTenantContext(ServerCallContext context, string? tenantId)
    {
        if (!_multiTenantEnabled)
        {
            return;
        }

        EnsureMultiTenantLicensed();
        HttpContext? httpContext = TryGetHttpContext(context);
        string? claimTenantId = httpContext?.User.FindFirst(ClaimConstants.TenantId)?.Value;
        bool requireTenantClaim = _requireTenantClaimForAuthenticatedUser && httpContext?.User.Identity?.IsAuthenticated == true;
        if (!TenantSecurityValidator.TryValidate(tenantId, claimTenantId, tenantId, requireTenantClaim, out string? tenantError))
        {
            throw tenantError switch
            {
                TenantSecurityValidator.MissingTenantContext => new RpcException(
                    new Status(StatusCode.Unauthenticated, "Tenant ID is required.")),
                TenantSecurityValidator.MissingTenantClaim => new RpcException(
                    new Status(StatusCode.Unauthenticated, "Tenant claim is required.")),
                _ => new RpcException(new Status(StatusCode.PermissionDenied, "Tenant mismatch."))
            };
        }
    }

    private void EnforceRateLimit(string? tenantId, string? apiKey)
    {
        if (_rateLimiter.TryConsume(apiKey, tenantId, _serverConfig.RateLimit, out string? _))
        {
            return;
        }

        throw new RpcException(new Status(StatusCode.ResourceExhausted, "[RATE_LIMIT] gRPC request rate exceeded."));
    }

    private void ValidateMutualTls(ServerCallContext context)
    {
        if (!_serverConfig.RequireMutualTls)
        {
            return;
        }

        System.Security.Cryptography.X509Certificates.X509Certificate2? cert =
            (TryGetHttpContext(context)?.Connection.ClientCertificate) ?? throw new RpcException(
                new Status(StatusCode.Unauthenticated, "[SECURITY] Client certificate is required."));

        string[] allowed = _serverConfig.AllowedClientCertificateThumbprints;
        if (allowed.Length == 0)
        {
            return;
        }

        string? thumbprint = cert.Thumbprint?.Replace(":", string.Empty, StringComparison.Ordinal);
        bool matched = !string.IsNullOrWhiteSpace(thumbprint) &&
                       allowed.Any(x =>
                           string.Equals(x.Replace(":", string.Empty, StringComparison.Ordinal), thumbprint, StringComparison.OrdinalIgnoreCase));
        if (!matched)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "[SECURITY] Client certificate is not trusted."));
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
            throw new RpcException(new Status(StatusCode.PermissionDenied,
                "[LICENSE] Feature 'grpc' is not available under your current license."));
        }
    }

    private void EnsureMultiTenantLicensed()
    {
        if (_licenseGuard is not null)
        {
            _licenseGuard.EnsureFeature(FreeTierFeatures.Premium.MultiTenant);
            return;
        }

        if (!_licenseState.HasFeature(FreeTierFeatures.Premium.MultiTenant))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied,
                "[LICENSE] Feature 'multi-tenant' is not available under your current license."));
        }
    }

    private static Activity? StartActivity(string method, string callType, string? tenantId)
    {
        Activity? activity = GrpcRuntimeTelemetry.ActivitySource.StartActivity(method, ActivityKind.Server);
        activity?.SetTag("grpc.method", method);
        activity?.SetTag("grpc.call_type", callType);
        activity?.SetTag("tenant.id", tenantId ?? string.Empty);
        return activity;
    }

    private static HttpContext? TryGetHttpContext(ServerCallContext context)
    {
        if (context.UserState.TryGetValue("__HttpContext", out object? value) && value is HttpContext httpContext)
        {
            return httpContext;
        }

        try
        {
            return context.GetHttpContext();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

}

/// <summary>
/// Helpers for reading gRPC metadata values.
/// </summary>
public static class MetadataExtensions
{
    /// <summary>
    /// Gets the first metadata value for the provided key.
    /// </summary>
    public static string? GetValue(this Metadata metadata, string key)
    {
        Metadata.Entry? entry = metadata.FirstOrDefault(m => m.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return entry?.Value;
    }
}
