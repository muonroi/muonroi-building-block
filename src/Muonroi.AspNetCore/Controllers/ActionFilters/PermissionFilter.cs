namespace Muonroi.AspNetCore.Controllers.ActionFilters;

public class PermissionFilter<TPermission>(ILogger<PermissionFilter<TPermission>> logger)
    : IAsyncActionFilter where TPermission : Enum
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Endpoint? endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() != null)
        {
            await next();
            return;
        }

        IReadOnlyList<PermissionAttribute<TPermission>>? permissionAttributes = endpoint?.Metadata.GetOrderedMetadata<PermissionAttribute<TPermission>>();

        if (permissionAttributes == null || !permissionAttributes.Any())
        {
            await next();
            return;
        }

        IServiceProvider? services = context.HttpContext.RequestServices;
        services?.GetService<ILicenseGuard>()?.EnsureFeature(FreeTierFeatures.Premium.AdvancedAuth);
        Muonroi.Tenancy.Core.Legacy.MultiTenantConfigs? multiTenantOptions =
            services?.GetService<IOptions<Muonroi.Tenancy.Core.Legacy.MultiTenantConfigs>>()?.Value;

        long? userPermissionsBitmask = GetPermissionsFromToken(context.HttpContext.User);

        string? userId = context.HttpContext.User.FindFirst(ClaimConstants.UserIdentifier)?.Value;
        string? tenantId = TenantContext.CurrentTenantId;
        string? claimTenantId = context.HttpContext.User.FindFirst(ClaimConstants.TenantId)?.Value;

        bool shouldEnforceTenant = multiTenantOptions?.Enabled == true ||
                                  (!string.IsNullOrWhiteSpace(claimTenantId) &&
                                   !string.IsNullOrWhiteSpace(tenantId));
        bool requireTenantClaim = multiTenantOptions?.RequireTenantClaimForAuthenticatedUser == true;
        if (shouldEnforceTenant &&
            !TenantSecurityValidator.TryValidate(tenantId, claimTenantId, null, requireTenantClaim, out string? tenantError))
        {
            logger.LogWarning(
                "Tenant validation failed ({ErrorCode}) while checking bitmask permission for user {User}. ClaimTenant={ClaimTenant}, ContextTenant={ContextTenant}",
                tenantError,
                userId,
                claimTenantId,
                tenantId);
            throw new PermissionDeniedException("Tenant validation failed");
        }

        IMPolicyDecisionService? policyDecisionService = services?.GetService<IMPolicyDecisionService>();
        if (policyDecisionService?.IsEnabled == true)
        {
            MPolicyDecisionRequest pdpRequest = BuildPolicyDecisionRequest(
                context.HttpContext,
                userId,
                tenantId,
                userPermissionsBitmask,
                permissionAttributes);
            MPolicyDecisionResult pdpDecision = await policyDecisionService.EvaluateAsync(pdpRequest, context.HttpContext.RequestAborted);
            if (pdpDecision.IsAuthoritative)
            {
                if (!pdpDecision.IsAllowed)
                {
                    logger.LogWarning(
                        "PDP denied permission for user {User} in tenant {Tenant}. Source={Source}, Correlation={Correlation}",
                        userId,
                        tenantId,
                        pdpDecision.DecisionSource,
                        pdpRequest.CorrelationId);
                    throw new PermissionDeniedException("Permission denied");
                }

                await next();
                return;
            }
        }

        if (userPermissionsBitmask == null)
        {
            logger.LogWarning(
                "User permissions missing for {User} in tenant {Tenant}",
                userId,
                tenantId);
            throw new PermissionDeniedException("User permissions missing");
        }

        if (!IsAuthorized(permissionAttributes, userPermissionsBitmask.Value))
        {
            logger.LogWarning(
                "Permission denied for user {User} in tenant {Tenant}",
                userId,
                tenantId);
            throw new PermissionDeniedException("Permission denied");
        }

        await next();
    }

    private static long? GetPermissionsFromToken(ClaimsPrincipal userClaims)
    {
        string? permissionClaim = userClaims.FindFirst(ClaimConstants.Permission)?.Value;

        return long.TryParse(permissionClaim, out long permissionsBitmask) ? permissionsBitmask : null;
    }

    private static bool HasPermission(long userPermissions, long requiredPermission)
    {
        return (userPermissions & requiredPermission) == requiredPermission;
    }

    private static bool IsAuthorized(IReadOnlyList<PermissionAttribute<TPermission>> attributes, long userPermissions)
    {
        List<long> value = [.. attributes
            .Where(attribute => attribute.MatchMode == PermissionMatchMode.Any)
            .Select(attribute => Convert.ToInt64(attribute.RequiredPermission))];
        List<long> anyMode = value;

        List<long> allMode = [.. attributes
            .Where(attribute => attribute.MatchMode == PermissionMatchMode.All)
            .Select(attribute => Convert.ToInt64(attribute.RequiredPermission))];

        bool hasAll = allMode.Count == 0 || allMode.All(required => HasPermission(userPermissions, required));
        bool hasAny = anyMode.Count == 0 || anyMode.Any(required => HasPermission(userPermissions, required));

        return hasAll && hasAny;
    }

    private static MPolicyDecisionRequest BuildPolicyDecisionRequest(
        HttpContext httpContext,
        string? userId,
        string? tenantId,
        long? userPermissionsBitmask,
        IReadOnlyList<PermissionAttribute<TPermission>> attributes)
    {
        string[] anyPermissions = attributes
            .Where(attribute => attribute.MatchMode == PermissionMatchMode.Any)
            .Select(attribute => attribute.RequiredPermission.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray()!;

        string[] allPermissions = attributes
            .Where(attribute => attribute.MatchMode == PermissionMatchMode.All)
            .Select(attribute => attribute.RequiredPermission.ToString())
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
            DecisionType = "permission-bitmask",
            UserId = userId,
            TenantId = tenantId,
            CorrelationId = ResolveCorrelationId(httpContext),
            Action = httpContext.Request.Method,
            Resource = httpContext.Request.Path.Value,
            RequiredAnyPermissions = anyPermissions,
            RequiredAllPermissions = allPermissions,
            UserPermissionsBitmask = userPermissionsBitmask,
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
