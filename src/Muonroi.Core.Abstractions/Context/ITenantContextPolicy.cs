using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Core.Abstractions.Context;

/// <summary>
/// Defines a policy for resolving and validating the tenant and user context.
/// </summary>
public interface ITenantContextPolicy
{
    /// <summary>
    /// Gets a value indicating whether a tenant identifier is required.
    /// </summary>
    bool IsTenantRequired { get; }

    /// <summary>
    /// Gets a value indicating whether a user identifier is required.
    /// </summary>
    bool IsUserRequired { get; }

    /// <summary>
    /// Resolves and validates the execution context based on the current policy.
    /// </summary>
    /// <param name="context">The context to resolve and validate.</param>
    /// <returns>The resolved and validated <see cref="ISystemExecutionContext"/>.</returns>
    ISystemExecutionContext ResolveAndValidate(ISystemExecutionContext context);
}

/// <summary>
/// A default implementation of <see cref="ITenantContextPolicy"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DefaultTenantContextPolicy"/> class.
/// </remarks>
/// <param name="resolver">The context resolver.</param>
public sealed class DefaultTenantContextPolicy(IContextResolver resolver) : ITenantContextPolicy
{
    private readonly IContextResolver _resolver = MGuard.NotNull(resolver);
    private static readonly bool TenancyInstalled = IsAssemblyLoaded("Muonroi.Tenancy");
    private static readonly bool AuthInstalled = IsAssemblyLoaded("Muonroi.Auth");

    /// <summary>
    /// Gets a value indicating whether a tenant identifier is required.
    /// </summary>
    public bool IsTenantRequired => TenancyInstalled;

    /// <summary>
    /// Gets a value indicating whether a user identifier is required.
    /// </summary>
    public bool IsUserRequired => AuthInstalled;

    /// <summary>
    /// Resolves and validates the execution context.
    /// </summary>
    /// <param name="context">The context to resolve and validate.</param>
    /// <returns>The resolved and validated context.</returns>
    /// <exception cref="MArgumentException">Thrown if context is null.</exception>
    /// <exception cref="MissingTenantContextException">Thrown if tenant context is missing but required.</exception>
    /// <exception cref="MissingUserContextException">Thrown if user context is missing but required.</exception>
    public ISystemExecutionContext ResolveAndValidate(ISystemExecutionContext context)
    {
        MGuard.NotNull(context);
        if (context is not SystemExecutionContext concrete)
        {
            concrete = new SystemExecutionContext(
                tenantId: context.TenantId,
                userId: context.UserId,
                username: context.Username,
                correlationId: context.CorrelationId,
                accessToken: context.AccessToken,
                apiKey: context.ApiKey,
                isAuthenticated: context.IsAuthenticated,
                permissions: context.Permissions,
                sourceType: context.SourceType);
        }

        string? tenantId = concrete.TenantId ?? _resolver.ResolveTenantId();
        string? userId = concrete.UserId ?? _resolver.ResolveUserId();
        string? username = concrete.Username ?? _resolver.ResolveUsername();
        bool hasAccessToken = !string.IsNullOrWhiteSpace(concrete.AccessToken);

        if (IsTenantRequired && string.IsNullOrWhiteSpace(tenantId))
        {
            throw new MissingTenantContextException(
                $"Missing tenant context for source '{concrete.SourceType}'.");
        }

        if (IsUserRequired && (string.IsNullOrWhiteSpace(userId) || !hasAccessToken))
        {
            throw new MissingUserContextException(
                $"Missing user/auth context for source '{concrete.SourceType}'.");
        }

        return concrete.With(
            tenantId: tenantId,
            userId: userId,
            username: username,
            isAuthenticated: concrete.IsAuthenticated || (!string.IsNullOrWhiteSpace(userId) && hasAccessToken));
    }

    private static bool IsAssemblyLoaded(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Any(x => string.Equals(x.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Exception thrown when tenant context is missing but required by the policy.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MissingTenantContextException"/> class.
/// </remarks>
/// <param name="message">The error message.</param>
public sealed class MissingTenantContextException(string message) : MException("MISSING_TENANT_CONTEXT", message, MExceptionCategory.Security, 400)
{
}

/// <summary>
/// Exception thrown when user context is missing but required by the policy.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MissingUserContextException"/> class.
/// </remarks>
/// <param name="message">The error message.</param>
public sealed class MissingUserContextException(string message) : MException("MISSING_USER_CONTEXT", message, MExceptionCategory.Security, 400)
{
}
