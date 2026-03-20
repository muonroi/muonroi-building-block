using Muonroi.Core.Abstractions.Context;

namespace Muonroi.AspNetCore.Middleware;

/// <summary>
/// Middleware for handling JWT-based authentication and context resolution.
/// </summary>
/// <param name="next">The next delegate in the middleware pipeline.</param>
/// <param name="callbackVerifyToken">The callback function to verify the token.</param>
/// <param name="executionContextAccessor">The accessor for system execution context.</param>
/// <param name="tenantContextPolicy">The policy for resolving and validating tenant context.</param>
/// <param name="logScopeFactory">The factory for creating log scopes.</param>
public class JwtMiddleware(
    RequestDelegate next,
    Func<IServiceProvider, HttpContext, Task<IAuthenticateInfoContext>> callbackVerifyToken,
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy,
    ILogScopeFactory? logScopeFactory = null)
{
    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task that represents the completion of the middleware invocation.</returns>
    public async Task Invoke(HttpContext context, IServiceProvider serviceProvider)
    {
        if (IsAnonymousPath(context))
        {
            await next(context);
            return;
        }

        IHeaderDictionary headers = context.Request.Headers;

        // Auto-add Bearer if missing
        if (headers.TryGetValue("Authorization", out StringValues authValues) && authValues.Count > 0)
        {
            string token = authValues.ToString();
            if (!string.IsNullOrEmpty(token) && !token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                headers.Authorization = $"Bearer {token}";
            }
        }

        // Security: Remove sensitive identity headers from the request to prevent spoofing
        _ = headers.Remove(nameof(MAuthenticateInfoContext.CurrentUserGuid));
        _ = headers.Remove(nameof(MAuthenticateInfoContext.CurrentUsername));
        _ = headers.Remove(nameof(MAuthenticateInfoContext.Permission));
        _ = headers.Remove(nameof(MAuthenticateInfoContext.TokenValidityKey));

        if (IsAllowAnonymous(context))
        {
            AddHeader(headers, nameof(MAuthenticateInfoContext.CorrelationId), Guid.NewGuid().ToString());
            SystemExecutionContext anonymousContext = new(
                tenantId: null,
                userId: null,
                username: null,
                correlationId: headers[nameof(MAuthenticateInfoContext.CorrelationId)].FirstOrDefault() ?? Guid.NewGuid().ToString("N"),
                accessToken: null,
                apiKey: null,
                isAuthenticated: false,
                permissions: [],
                sourceType: "http");
            using SystemExecutionContextScope scope = new(executionContextAccessor, anonymousContext);
            await next(context);

            return;
        }

        IAuthenticateInfoContext verifyToken = await callbackVerifyToken(serviceProvider, context);

        if (!verifyToken.IsAuthenticated)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        _ = headers.TryGetValue("Accept-Language", out StringValues language);
        _ = headers.TryGetValue(nameof(MAuthenticateInfoContext.CorrelationId), out StringValues correlationIds);
        AddHeader(headers, nameof(MAuthenticateInfoContext.CurrentUserGuid), verifyToken.CurrentUserGuid);
        AddHeader(headers, nameof(MAuthenticateInfoContext.TokenValidityKey), verifyToken.TokenValidityKey);
        AddHeader(headers, nameof(MAuthenticateInfoContext.CurrentUsername), verifyToken.CurrentUsername);
        AddHeader(headers, nameof(MAuthenticateInfoContext.Permission), verifyToken.Permission ?? string.Empty);
        AddHeader(headers, "Accept-Language", language.FirstOrDefault() ?? "vi-VN");
        if (correlationIds.Count == 0)
        {
            AddHeader(headers, nameof(MAuthenticateInfoContext.CorrelationId), Guid.NewGuid().ToString());
        }

        List<string> permissions = string.IsNullOrWhiteSpace(verifyToken.Permission)
            ? []
            : [.. verifyToken.Permission!
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)];

        SystemExecutionContext rawContext = new(
            tenantId: verifyToken.TenantId,
            userId: verifyToken.CurrentUserGuid,
            username: verifyToken.CurrentUsername,
            correlationId: headers[nameof(MAuthenticateInfoContext.CorrelationId)].FirstOrDefault() ?? Guid.NewGuid().ToString("N"),
            accessToken: verifyToken.AccessToken,
            apiKey: verifyToken.ApiKey,
            isAuthenticated: verifyToken.IsAuthenticated,
            permissions: permissions,
            sourceType: "http");
        SystemExecutionContext resolvedContext = (SystemExecutionContext)tenantContextPolicy.ResolveAndValidate(rawContext);

        List<Claim> claims =
        [
            new(nameof(MAuthenticateInfoContext.CurrentUsername), verifyToken.CurrentUsername),
            new(nameof(MAuthenticateInfoContext.CurrentUserGuid), verifyToken.CurrentUserGuid)
        ];

        if (!string.IsNullOrWhiteSpace(resolvedContext.TenantId))
        {
            claims.Add(new Claim(ClaimConstants.TenantId, resolvedContext.TenantId));
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme));
        using SystemExecutionContextScope scopeWithContext = new(executionContextAccessor, resolvedContext);
        using ContextMirrorScope contextMirrorScope = ContextMirrorScope.Apply(resolvedContext, logScopeFactory);
        await next(context);
    }

    private static bool IsAllowAnonymous(HttpContext httpContext)
    {
        Endpoint? endpoint = httpContext.GetEndpoint();
        if (endpoint is null)
        {
            return true;
        }

        AllowAnonymousAttribute? endpointMetadata = endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>();
        AuthorizeAttribute? authorize = endpoint.Metadata.GetMetadata<AuthorizeAttribute>();

        return (endpointMetadata is null && authorize is null) || endpointMetadata is not null;
    }

    private static bool IsAnonymousPath(HttpContext httpContext)
    {
        string path = httpContext.Request.Path.Value ?? string.Empty;
        return path.Contains("/swagger", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/health", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddHeader(IHeaderDictionary headers, string key, string value)
    {
        _ = headers.Remove(key);
        headers.Append(key, value);
    }
}
