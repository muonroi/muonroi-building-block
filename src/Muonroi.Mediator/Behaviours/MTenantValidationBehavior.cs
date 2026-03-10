using Muonroi.Core.Abstractions.Context;
using Muonroi.Mediator.Exceptions;
using Muonroi.Mediator.Mediator.Context;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Validates and hydrates the Tenant context for requests implementing IMTenantRequest.
/// This behavior acts as the "Firewall" for Multi-tenancy.
/// </summary>
public sealed class MTenantValidationBehavior<TRequest, TResponse>(
    ISystemExecutionContextAccessor contextAccessor,
    IRequestContextBag contextBag)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public const string TenantIdKey = "Muonroi:TenantId";
    public const string UserIdKey = "Muonroi:UserId";

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ISystemExecutionContext ctx = contextAccessor.Get();
        
        // Hydrate the context bag if data exists in accessor
        if (!string.IsNullOrWhiteSpace(ctx.TenantId))
        {
            contextBag.Set(TenantIdKey, ctx.TenantId);
        }
        
        if (!string.IsNullOrWhiteSpace(ctx.UserId))
        {
            contextBag.Set(UserIdKey, ctx.UserId);
        }

        // Firewall check: If request requires tenant, but context is missing, block it.
        if (request is IMTenantRequest<TResponse>)
        {
            if (string.IsNullOrWhiteSpace(ctx.TenantId))
            {
                throw new MUnauthorizedException(
                    $"Request '{typeof(TRequest).Name}' is tenant-protected but no TenantId was found in the execution context.");
            }
        }

        return await next();
    }
}
