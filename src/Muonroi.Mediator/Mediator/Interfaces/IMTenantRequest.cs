namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Marker interface for requests that require a validated tenant context.
/// <see cref="Muonroi.Mediator.Behaviours.MTenantValidationBehavior{TRequest,TResponse}"/>
/// rejects requests where <c>ISystemExecutionContextAccessor.Get().TenantId</c> is null or empty.
/// </summary>
public interface IMTenantRequest<out TResponse> : IRequest<TResponse> { }
