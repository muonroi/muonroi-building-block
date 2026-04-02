using Microsoft.Extensions.Options;

namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Server-side gRPC interceptor that extracts the SiteCode from incoming requests.
/// <para>
/// Resolution order:
/// <list type="number">
///   <item>gRPC metadata key (configured via <see cref="SiteGrpcOptions.MetadataKey"/>)</item>
///   <item>HTTP header fallback (configured via <see cref="SiteGrpcOptions.HttpHeaderFallbackKey"/>)</item>
/// </list>
/// Behavior when SiteCode not found depends on <see cref="SiteGrpcOptions.Required"/>.
/// </para>
/// <para>
/// The resolved SiteCode is stored in <see cref="ISiteCodeHolder"/> (scoped) and
/// <c>HttpContext.Items["__site_code"]</c> (fallback for non-DI scenarios).
/// </para>
/// <para>
/// Consumer configures the metadata key:
/// <code>
/// services.AddSiteGrpcServices(o => o.MetadataKey = "SiteCode"); // PascalCase convention
/// </code>
/// </para>
/// </summary>
public sealed class SiteCodeGrpcInterceptor : Interceptor
{
    /// <summary>The HttpContext.Items key used as fallback storage.</summary>
    internal const string HttpContextItemKey = "__site_code";

    private readonly SiteGrpcOptions _options;

    /// <summary>
    /// Creates a new interceptor with the specified options.
    /// </summary>
    public SiteCodeGrpcInterceptor(IOptions<SiteGrpcOptions> options)
    {
        _options = options.Value;
    }

    private void ResolveSiteCode(ServerCallContext context)
    {
        // 1. Try gRPC metadata with configured key
        string? siteCode = context.RequestHeaders
            .FirstOrDefault(e => string.Equals(e.Key, _options.MetadataKey, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        // 2. Fallback to HTTP header (if configured)
        if (string.IsNullOrEmpty(siteCode) && _options.HttpHeaderFallbackKey is not null)
        {
            HttpContext httpContext = context.GetHttpContext();
            siteCode = httpContext.Request.Headers[_options.HttpHeaderFallbackKey].FirstOrDefault();
        }

        // 3. Handle missing SiteCode
        if (string.IsNullOrEmpty(siteCode))
        {
            if (_options.Required)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    $"Missing SiteCode in gRPC metadata key '{_options.MetadataKey}'" +
                    (_options.HttpHeaderFallbackKey is not null
                        ? $" or HTTP header '{_options.HttpHeaderFallbackKey}'"
                        : string.Empty) +
                    ". Every gRPC request must include a SiteCode."));
            }

            return; // Not required — continue without setting SiteCode
        }

        // 4. Store in scoped holder + HttpContext.Items
        HttpContext ctx = context.GetHttpContext();

        ISiteCodeHolder? holder = ctx.RequestServices.GetService<ISiteCodeHolder>();
        if (holder is not null)
        {
            holder.SiteCode = siteCode;
        }

        ctx.Items[HttpContextItemKey] = siteCode;
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ResolveSiteCode(context);
        return await continuation(request, context);
    }

    /// <inheritdoc />
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ResolveSiteCode(context);
        await continuation(request, responseStream, context);
    }

    /// <inheritdoc />
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ResolveSiteCode(context);
        return await continuation(requestStream, context);
    }

    /// <inheritdoc />
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ResolveSiteCode(context);
        await continuation(requestStream, responseStream, context);
    }
}
