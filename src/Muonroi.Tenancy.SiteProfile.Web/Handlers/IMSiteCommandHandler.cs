using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Tenancy.SiteProfile.Web.Handlers;

/// <summary>
/// Marker interface for per-site MediatR command handlers registered in the ecosystem keyed DI system.
///
/// Consumers derive from <see cref="MSiteCommandHandler{TRequest,TResponse}"/> and register each
/// site-specific implementation using a keyed registration keyed by SiteId:
///
/// <example>
/// <code>
/// // TciSiteProfile.RegisterServices():
/// services.AddKeyedScoped&lt;IRequestHandler&lt;CreateOrderCommand, CreateOrderResponse&gt;,
///     TciCreateOrderHandler&gt;("TCI");
///
/// // Program.cs:
/// services.AddSiteCommandHandler&lt;CreateOrderCommand, CreateOrderResponse&gt;();
/// </code>
/// </example>
///
/// The <c>AddSiteCommandHandler&lt;TCmd, TResp&gt;()</c> extension then registers a scoped factory
/// via <c>AddSiteResolvedService</c> that resolves the correct keyed handler based on the current
/// site context at runtime — no SiteCode if/switch branching required.
/// </summary>
/// <typeparam name="TRequest">The command/request type implementing <see cref="IRequest{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public interface IMSiteCommandHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
}
