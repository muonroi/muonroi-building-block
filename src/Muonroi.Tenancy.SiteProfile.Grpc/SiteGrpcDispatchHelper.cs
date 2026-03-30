using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Server-side dispatch helper that resolves the correct site-specific gRPC handler per request
/// using keyed DI and delegates RPC calls to it.
///
/// <para>
/// Resolution order:
/// <list type="number">
///   <item>Try exact <c>siteCode</c> match from <see cref="ISiteCodeHolder.SiteCode"/></item>
///   <item>Fall back to keyed service registered under <c>"default"</c></item>
///   <item>Throw <see cref="RpcException"/> with descriptive message if neither found</item>
/// </list>
/// </para>
///
/// <para>
/// Consumer writes a thin dispatcher proxy that inherits from the proto-generated base class
/// and delegates each RPC to this helper:
/// <code>
/// public class FcdDispatcher : FullContainerDeliveryBase
/// {
///     private readonly SiteGrpcDispatchHelper&lt;FullContainerDeliveryBase&gt; _helper;
///
///     public FcdDispatcher(SiteGrpcDispatchHelper&lt;FullContainerDeliveryBase&gt; helper)
///         => _helper = helper;
///
///     public override Task&lt;CreateV4Reply&gt; CreateV4(CreateV4Request request, ServerCallContext context)
///         => _helper.DispatchAsync(context, (h, ctx) =&gt; h.CreateV4(request, ctx));
/// }
/// </code>
/// </para>
///
/// <para>
/// Register via:
/// <code>
/// services.AddSiteGrpcHandler&lt;FullContainerDeliveryBase, TciFcdService&gt;("TCI");
/// services.AddSiteGrpcHandler&lt;FullContainerDeliveryBase, SharedFcdService&gt;("default");
/// services.AddSiteGrpcDispatcher&lt;FullContainerDeliveryBase&gt;();
/// </code>
/// </para>
///
/// Lifetime: Scoped — registered once per gRPC request scope.
/// </summary>
/// <typeparam name="TServiceBase">
/// The proto-generated gRPC service base class (e.g., <c>FullContainerDeliveryBase</c>).
/// Must be a class (not interface) — gRPC generated base classes use <c>abstract class</c>.
/// </typeparam>
public sealed class SiteGrpcDispatchHelper<TServiceBase>
    where TServiceBase : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISiteCodeHolder _siteCodeHolder;
    private readonly IMLog<SiteGrpcDispatchHelper<TServiceBase>> _log;

    /// <summary>
    /// Creates a new dispatch helper.
    /// </summary>
    /// <param name="serviceProvider">DI container for keyed service resolution.</param>
    /// <param name="siteCodeHolder">Scoped holder carrying the current request's SiteCode (set by <see cref="SiteCodeGrpcInterceptor"/>).</param>
    /// <param name="log">Structured logger for dispatch diagnostics.</param>
    public SiteGrpcDispatchHelper(
        IServiceProvider serviceProvider,
        ISiteCodeHolder siteCodeHolder,
        IMLog<SiteGrpcDispatchHelper<TServiceBase>> log)
    {
        _serviceProvider = serviceProvider;
        _siteCodeHolder = siteCodeHolder;
        _log = log;
    }

    /// <summary>
    /// Resolves the site-specific handler and delegates the RPC call to it.
    /// </summary>
    /// <typeparam name="TResponse">The RPC response type.</typeparam>
    /// <param name="context">The server call context from gRPC runtime.</param>
    /// <param name="rpcCall">
    /// Delegate that invokes the actual RPC on the resolved handler.
    /// Receives the resolved <typeparamref name="TServiceBase"/> and the <see cref="ServerCallContext"/>.
    /// </param>
    /// <param name="rpcName">
    /// The RPC method name for logging. Automatically populated from the caller's method name
    /// via <see cref="CallerMemberNameAttribute"/> — do not pass explicitly.
    /// </param>
    /// <returns>The response from the resolved handler.</returns>
    /// <exception cref="RpcException">
    /// Thrown with <see cref="StatusCode.Internal"/> if no handler is registered for the
    /// resolved site code AND no "default" fallback is registered.
    /// </exception>
    public async Task<TResponse> DispatchAsync<TResponse>(
        ServerCallContext context,
        Func<TServiceBase, ServerCallContext, Task<TResponse>> rpcCall,
        [CallerMemberName] string? rpcName = null)
    {
        // SiteCode is already set by SiteCodeGrpcInterceptor before this method is called.
        // If null (interceptor not in pipeline or SiteCode not required), fall through to "default".
        string siteCode = _siteCodeHolder.SiteCode ?? "default";

        // 1. Try exact site-specific handler
        TServiceBase? handler = _serviceProvider.GetKeyedService<TServiceBase>(siteCode);

        // 2. Fall back to "default" if site-specific not registered
        if (handler is null && !string.Equals(siteCode, "default", StringComparison.OrdinalIgnoreCase))
        {
            handler = _serviceProvider.GetKeyedService<TServiceBase>("default");
        }

        // 3. No handler found — throw descriptive error
        if (handler is null)
        {
            throw new RpcException(
                new Status(
                    StatusCode.Internal,
                    $"No gRPC handler registered for site '{siteCode}' or 'default' fallback " +
                    $"for service type '{typeof(TServiceBase).Name}'. " +
                    $"Register via services.AddSiteGrpcHandler<{typeof(TServiceBase).Name}, TImpl>(siteId)."));
        }

        _log.LogDebug(
            "GRPC-DISPATCH: {SiteCode} -> {HandlerType}.{RpcName}",
            siteCode,
            handler.GetType().Name,
            rpcName);

        return await rpcCall(handler, context);
    }
}
