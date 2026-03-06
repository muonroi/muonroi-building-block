namespace Muonroi.Core.Abstractions.Context;

public interface ITenantContextPolicy
{
    bool IsTenantRequired { get; }
    bool IsUserRequired { get; }
    ISystemExecutionContext ResolveAndValidate(ISystemExecutionContext context);
}

public sealed class DefaultTenantContextPolicy(IContextResolver resolver) : ITenantContextPolicy
{
    private readonly IContextResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private static readonly bool TenancyInstalled = IsAssemblyLoaded("Muonroi.Tenancy");
    private static readonly bool AuthInstalled = IsAssemblyLoaded("Muonroi.Auth");

    public bool IsTenantRequired => TenancyInstalled;
    public bool IsUserRequired => AuthInstalled;

    public ISystemExecutionContext ResolveAndValidate(ISystemExecutionContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
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

public sealed class MissingTenantContextException : InvalidOperationException
{
    public MissingTenantContextException(string message) : base(message)
    {
    }
}

public sealed class MissingUserContextException : InvalidOperationException
{
    public MissingUserContextException(string message) : base(message)
    {
    }
}
