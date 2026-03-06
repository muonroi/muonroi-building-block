namespace Muonroi.AspNetCore.Controllers.ActionFilters;

public class AuthorizePermissionFilter<TDbContext>(
    TDbContext dbContext,
    IMultiLevelCacheService cacheService,
    ILogger<AuthorizePermissionFilter<TDbContext>> logger) : IAsyncActionFilter
    where TDbContext : MDbContext
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Endpoint? endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() != null)
        {
            _ = await next();
            return;
        }

        IReadOnlyList<AuthorizePermissionAttribute>? attributes = endpoint?.Metadata.GetOrderedMetadata<AuthorizePermissionAttribute>();
        if (attributes == null || attributes.Count == 0)
        {
            _ = await next();
            return;
        }

        IServiceProvider? services = context.HttpContext.RequestServices;
        services?.GetService<ILicenseGuard>()?.EnsureFeature(FreeTierFeatures.Premium.AdvancedAuth);
        Muonroi.Tenancy.Core.Legacy.MultiTenantConfigs? multiTenantOptions =
            services?.GetService<IOptions<Muonroi.Tenancy.Core.Legacy.MultiTenantConfigs>>()?.Value;

        string? userIdString = context.HttpContext.User.FindFirst(ClaimConstants.UserIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out Guid userId))
        {
            logger.LogWarning("User id invalid");
            throw new PermissionDeniedException("Invalid user id");
        }

        string? claimTenantId = context.HttpContext.User.FindFirst(ClaimConstants.TenantId)?.Value;
        string? currentTenantId = TenantContext.CurrentTenantId;
        bool shouldEnforceTenant = multiTenantOptions?.Enabled == true ||
                                  (!string.IsNullOrWhiteSpace(claimTenantId) &&
                                   !string.IsNullOrWhiteSpace(currentTenantId));
        bool requireTenantClaim = multiTenantOptions?.RequireTenantClaimForAuthenticatedUser == true;
        if (shouldEnforceTenant &&
            !TenantSecurityValidator.TryValidate(currentTenantId, claimTenantId, null, requireTenantClaim,
                out string? tenantError))
        {
            logger.LogWarning(
                "Tenant validation failed ({ErrorCode}) while checking permission for user {User}. ClaimTenant={ClaimTenant}, ContextTenant={ContextTenant}",
                tenantError,
                userId,
                claimTenantId,
                currentTenantId);
            throw new PermissionDeniedException("Tenant validation failed");
        }

        IMPolicyDecisionService? policyDecisionService = services?.GetService<IMPolicyDecisionService>();
        if (policyDecisionService?.IsEnabled == true)
        {
            MPolicyDecisionRequest pdpRequest = BuildPolicyDecisionRequest(
                context.HttpContext,
                userId,
                currentTenantId,
                attributes);
            MPolicyDecisionResult pdpDecision = await policyDecisionService.EvaluateAsync(pdpRequest, context.HttpContext.RequestAborted);
            if (pdpDecision.IsAuthoritative)
            {
                if (!pdpDecision.IsAllowed)
                {
                    logger.LogWarning(
                        "PDP denied permission for user {User} in tenant {Tenant}. Source={Source}, Correlation={Correlation}",
                        userId,
                        currentTenantId,
                        pdpDecision.DecisionSource,
                        pdpRequest.CorrelationId);
                    throw new PermissionDeniedException("Permission denied");
                }

                _ = await next();
                return;
            }
        }

        string cacheKey = RbacCacheKeys.UserPermissionsByEntityId(userId);
        List<string>? userPermissions = await cacheService.GetOrSetAsync(
            cacheKey,
            async () => await (from ur in dbContext.UserRoles.AsNoTracking()
                               join role in dbContext.Roles.AsNoTracking() on ur.RoleId equals role.EntityId
                               join user in dbContext.Users.AsNoTracking() on ur.UserId equals user.EntityId
                               join rp in dbContext.RolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
                               join p in dbContext.Permissions.AsNoTracking() on rp.PermissionId equals p.EntityId
                               where ur.UserId == userId
                                     && !ur.IsDeleted
                                     && !user.IsDeleted
                                     && !role.IsDeleted
                                     && !rp.IsDeleted
                                     && !p.IsDeleted
                               select p.Name).Distinct().ToListAsync(),
            15,
            context.HttpContext.RequestAborted);

        userPermissions ??= [];

        if (!IsAuthorized(attributes, userPermissions))
        {
            string required = string.Join(", ", attributes.Select(a => $"{a.PermissionKey}:{a.MatchMode}"));
            logger.LogWarning(
                "Permission denied for user {User} in tenant {Tenant}. Required={Required}",
                userId,
                currentTenantId,
                required);
            throw new PermissionDeniedException("Permission denied");
        }

        _ = await next();
    }

    private static bool IsAuthorized(
        IReadOnlyList<AuthorizePermissionAttribute> attributes,
        IReadOnlyCollection<string> userPermissions)
    {
        List<string> anyMode = [.. attributes
            .Where(attribute => attribute.MatchMode == PermissionMatchMode.Any)
            .Select(attribute => attribute.PermissionKey)];

        List<string> allMode = [.. attributes
            .Where(attribute => attribute.MatchMode == PermissionMatchMode.All)
            .Select(attribute => attribute.PermissionKey)];

        bool hasAll = allMode.Count == 0 ||
                     allMode.All(required => userPermissions.Contains(required, StringComparer.OrdinalIgnoreCase));
        bool hasAny = anyMode.Count == 0 ||
                     anyMode.Any(required => userPermissions.Contains(required, StringComparer.OrdinalIgnoreCase));

        return hasAll && hasAny;
    }

    private static MPolicyDecisionRequest BuildPolicyDecisionRequest(
        HttpContext httpContext,
        Guid userId,
        string? tenantId,
        IReadOnlyList<AuthorizePermissionAttribute> attributes)
    {
        string[] anyPermissions = attributes
            .Where(attribute => attribute.MatchMode == PermissionMatchMode.Any)
            .Select(attribute => attribute.PermissionKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray()!;

        string[] allPermissions = attributes
            .Where(attribute => attribute.MatchMode == PermissionMatchMode.All)
            .Select(attribute => attribute.PermissionKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray()!;

        Dictionary<string, string> claims = new(StringComparer.OrdinalIgnoreCase);
        foreach (Claim claim in httpContext.User.Claims)
        {
            if (!claims.ContainsKey(claim.Type))
            {
                claims[claim.Type] = claim.Value;
            }
        }

        MPolicyDecisionRequest request = new()
        {
            DecisionType = "permission-key",
            UserId = userId.ToString(),
            TenantId = tenantId,
            CorrelationId = ResolveCorrelationId(httpContext),
            Action = httpContext.Request.Method,
            Resource = httpContext.Request.Path.Value,
            RequiredAnyPermissions = anyPermissions,
            RequiredAllPermissions = allPermissions,
            Claims = claims
        };
        return request;
    }

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(CustomHeader.CorrelationId, out StringValues values))
        {
            string? header = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(header))
            {
                return header;
            }
        }

        return httpContext.TraceIdentifier;
    }
}
