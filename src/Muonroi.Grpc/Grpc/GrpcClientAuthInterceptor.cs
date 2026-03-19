using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Context;

namespace Muonroi.Grpc.Grpc;

/// <summary>
/// Represents forwarding options for outbound gRPC authentication metadata.
/// </summary>
public class GrpcClientAuthForwardingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the current execution context access token should be forwarded.
    /// </summary>
    public bool ForwardAuthToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current tenant identifier should be forwarded.
    /// </summary>
    public bool ForwardTenantId { get; set; } = true;
}

/// <summary>
/// Adds authorization, tenant, and correlation metadata to outbound gRPC calls.
/// </summary>
public class GrpcClientAuthInterceptor(
    ISystemExecutionContextAccessor contextAccessor,
    GrpcClientAuthForwardingOptions options)
    : Interceptor
{
    private const string AuthorizationHeaderName = "authorization";
    private readonly ISystemExecutionContextAccessor _contextAccessor =
        contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
    private readonly GrpcClientAuthForwardingOptions _options =
        options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Intercepts blocking unary calls and appends execution-context metadata.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The outgoing request.</param>
    /// <param name="context">The client interceptor context.</param>
    /// <param name="continuation">The continuation delegate.</param>
    /// <returns>The server response.</returns>
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, CreateContext(context));
    }

    /// <summary>
    /// Intercepts asynchronous unary calls and appends execution-context metadata.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The outgoing request.</param>
    /// <param name="context">The client interceptor context.</param>
    /// <param name="continuation">The continuation delegate.</param>
    /// <returns>The wrapped asynchronous call.</returns>
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, CreateContext(context));
    }

    /// <summary>
    /// Intercepts asynchronous client-streaming calls and appends execution-context metadata.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="context">The client interceptor context.</param>
    /// <param name="continuation">The continuation delegate.</param>
    /// <returns>The wrapped asynchronous call.</returns>
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(CreateContext(context));
    }

    /// <summary>
    /// Intercepts asynchronous server-streaming calls and appends execution-context metadata.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The outgoing request.</param>
    /// <param name="context">The client interceptor context.</param>
    /// <param name="continuation">The continuation delegate.</param>
    /// <returns>The wrapped asynchronous call.</returns>
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, CreateContext(context));
    }

    /// <summary>
    /// Intercepts asynchronous duplex-streaming calls and appends execution-context metadata.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="context">The client interceptor context.</param>
    /// <param name="continuation">The continuation delegate.</param>
    /// <returns>The wrapped asynchronous call.</returns>
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(CreateContext(context));
    }

    /// <summary>
    /// Creates a new client context with the configured forwarding headers.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="context">The current client context.</param>
    /// <returns>A client context with merged metadata headers.</returns>
    protected ClientInterceptorContext<TRequest, TResponse> CreateContext<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        Metadata headers = AppendMetadata(context.Options.Headers);
        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));
    }

    /// <summary>
    /// Merges execution-context headers with existing metadata.
    /// </summary>
    /// <param name="headers">The existing metadata.</param>
    /// <returns>The merged metadata collection.</returns>
    protected Metadata AppendMetadata(Metadata? headers)
    {
        Metadata metadata = CloneHeaders(headers);
        ISystemExecutionContext current = _contextAccessor.Get();

        if (!string.IsNullOrWhiteSpace(current.CorrelationId) && !ContainsHeader(metadata, CustomHeader.CorrelationId))
        {
            metadata.Add(CustomHeader.CorrelationId, current.CorrelationId);
        }

        if (_options.ForwardTenantId &&
            !string.IsNullOrWhiteSpace(current.TenantId) &&
            !ContainsHeader(metadata, CustomHeader.TenantId))
        {
            metadata.Add(CustomHeader.TenantId, current.TenantId);
        }

        if (_options.ForwardAuthToken &&
            !string.IsNullOrWhiteSpace(current.AccessToken) &&
            !ContainsHeader(metadata, AuthorizationHeaderName))
        {
            metadata.Add(AuthorizationHeaderName, FormatAuthorizationHeader(current.AccessToken));
        }

        return metadata;
    }

    private static Metadata CloneHeaders(Metadata? headers)
    {
        Metadata metadata = [];
        if (headers is null)
        {
            return metadata;
        }

        foreach (Metadata.Entry entry in headers)
        {
            if (entry.IsBinary)
            {
                metadata.Add(entry.Key, entry.ValueBytes);
                continue;
            }

            metadata.Add(entry.Key, entry.Value);
        }

        return metadata;
    }

    private static bool ContainsHeader(Metadata metadata, string key)
    {
        return metadata.Any(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatAuthorizationHeader(string accessToken)
    {
        string trimmed = accessToken.Trim();
        return trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"Bearer {trimmed}";
    }
}

internal sealed class GrpcClientAuthForwardingOptions<TClient> : GrpcClientAuthForwardingOptions
    where TClient : class
{
}

internal sealed class GrpcClientAuthInterceptor<TClient>(
    ISystemExecutionContextAccessor contextAccessor,
    GrpcClientAuthForwardingOptions<TClient> options)
    : GrpcClientAuthInterceptor(contextAccessor, options)
    where TClient : class
{
}
