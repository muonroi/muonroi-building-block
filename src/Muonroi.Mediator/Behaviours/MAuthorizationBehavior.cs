using Muonroi.Core.Abstractions.Context;
using Muonroi.Mediator.Exceptions;
using Muonroi.Mediator.Mediator.Attributes;
using Muonroi.Mediator.Mediator.Interfaces;
using System.Collections.Concurrent;

namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Enforces <see cref="MAuthorizeAttribute"/> constraints declared on <typeparamref name="TRequest"/>.
/// Roles: at least one required role must exist in context.
/// Permissions: ALL required permissions must exist in context.
/// Multiple <see cref="MAuthorizeAttribute"/> instances on the same class are ANDed together.
/// No-op when no attributes are present.
/// </summary>
public sealed class MAuthorizationBehavior<TRequest, TResponse>(
    ISystemExecutionContextAccessor contextAccessor)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    // Cache attribute lookups per request type to avoid repeated reflection
    private static readonly ConcurrentDictionary<Type, MAuthorizeAttribute[]> _attributeCache = new();

    /// <summary>
    /// Executes the Handle operation.
    /// </summary>
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        MAuthorizeAttribute[] attributes = _attributeCache.GetOrAdd(
            typeof(TRequest),
            static t => (MAuthorizeAttribute[])t.GetCustomAttributes(typeof(MAuthorizeAttribute), true));

        if (attributes.Length == 0)
            return next();

        ISystemExecutionContext ctx = contextAccessor.Get();
        IReadOnlyList<string> ctxPermissions = ctx.Permissions;

        List<string> missingRoles = [];
        List<string> missingPermissions = [];

        foreach (MAuthorizeAttribute attr in attributes)
        {
            // Roles: at least one must match (OR semantics per attribute)
            if (!string.IsNullOrWhiteSpace(attr.Roles))
            {
                string[] required = attr.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                bool hasRole = required.Any(r => ctxPermissions.Contains(r, StringComparer.OrdinalIgnoreCase));
                if (!hasRole)
                    missingRoles.AddRange(required);
            }

            // Permissions: ALL must match (AND semantics per attribute)
            if (!string.IsNullOrWhiteSpace(attr.Permissions))
            {
                string[] required = attr.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string p in required)
                {
                    if (!ctxPermissions.Contains(p, StringComparer.OrdinalIgnoreCase))
                        missingPermissions.Add(p);
                }
            }
        }

        if (missingRoles.Count > 0 || missingPermissions.Count > 0)
            throw new MForbiddenException(missingRoles, missingPermissions);

        return next();
    }
}
