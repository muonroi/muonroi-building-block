namespace Muonroi.AspNetCore.Controllers.ActionFilters;

/// <summary>
/// An action filter that performs permission-based authorization for the current request
/// by evaluating <see cref="AuthorizePermissionAttribute"/> metadata applied to the target endpoint.
/// </summary>
/// <typeparam name="TDbContext">
/// The application database context type used to resolve users, roles, and permissions.
/// </typeparam>
/// <remarks>
/// This filter supports a hybrid authorization flow:
/// <list type="number">
/// <item>
/// <description>
/// Skips authorization when the endpoint allows anonymous access.
/// </description>
/// </item>
/// <item>
/// <description>
/// Reads permission requirements from <see cref="AuthorizePermissionAttribute"/> metadata.
/// </description>
/// </item>
/// <item>
/// <description>
/// Validates tenant context when multi-tenant authorization is enabled.
/// </description>
/// </item>
/// <item>
/// <description>
/// Delegates authorization to a policy decision service when available and authoritative.
/// </description>
/// </item>
/// <item>
/// <description>
/// Falls back to local RBAC permission resolution using cached role-permission mappings from the database.
/// </description>
/// </item>
/// </list>
///
/// This filter is intended for advanced authorization scenarios in multi-tenant applications
/// where endpoint-level permission enforcement is required.
/// </remarks>
public class AuthorizePermissionFilter<TDbContext>(
    TDbContext dbContext,
    IMultiLevelCacheService cacheService,
    IMLog<AuthorizePermissionFilter<TDbContext>> logger) : IAsyncActionFilter
    where TDbContext : MDbContext
{
    /// <summary>
    /// Executes the action filter asynchronously to validate whether the current user
    /// has the required permissions to access the target endpoint.
    /// </summary>
    /// <param name="context">
    /// The <see cref="ActionExecutingContext"/> for the current request.
    /// </param>
    /// <param name="next">
    /// The delegate to execute the next action filter or the action itself if authorization succeeds.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous filter execution.
    /// </returns>
    /// <exception cref="PermissionDeniedException">
    /// Thrown when the current user identifier is invalid, tenant validation fails,
    /// or the user does not satisfy the required permission constraints.
    /// </exception>
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
            logger.Warn("User id invalid");
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
            logger.Warn(
                "Tenant validation failed ({ErrorCode}) while checking permission for user {User}. ClaimTenant={ClaimTenant}, ContextTenant={ContextTenant}",
                tenantError);
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
                    logger.Warn(
                        "PDP denied permission for user {User} in tenant {Tenant}. Source={Source}, Correlation={Correlation}",
                        pdpDecision.DecisionSource);
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
            logger.Warn(
                "Permission denied for user {User} in tenant {Tenant}. Required={Required}",
                required);
            throw new PermissionDeniedException("Permission denied");
        }

        _ = await next();
    }

    /// <summary>
    /// Determines whether the specified user permissions satisfy the required permission attributes.
    /// </summary>
    /// <param name="attributes">
    /// The permission attributes declared on the target endpoint.
    /// </param>
    /// <param name="userPermissions">
    /// The set of permissions currently granted to the user.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if all <see cref="PermissionMatchMode.All"/> permissions are present
    /// and at least one <see cref="PermissionMatchMode.Any"/> permission is present when required;
    /// otherwise, <see langword="false"/>.
    /// </returns>
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

    /// <summary>
    /// Builds a policy decision request from the current HTTP context and endpoint permission metadata.
    /// </summary>
    /// <param name="httpContext">
    /// The current HTTP context.
    /// </param>
    /// <param name="userId">
    /// The identifier of the authenticated user.
    /// </param>
    /// <param name="tenantId">
    /// The current tenant identifier associated with the request.
    /// </param>
    /// <param name="attributes">
    /// The permission attributes resolved from the target endpoint.
    /// </param>
    /// <returns>
    /// A populated <see cref="MPolicyDecisionRequest"/> instance for policy evaluation.
    /// </returns>
    /// <remarks>
    /// The generated request includes:
    /// <list type="bullet">
    /// <item><description>Decision type</description></item>
    /// <item><description>User and tenant identifiers</description></item>
    /// <item><description>Correlation identifier</description></item>
    /// <item><description>HTTP method and request path as action/resource context</description></item>
    /// <item><description>Required permissions grouped by match mode</description></item>
    /// <item><description>User claims</description></item>
    /// </list>
    /// </remarks>
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

    /// <summary>
    /// Resolves the correlation identifier for the current request.
    /// </summary>
    /// <param name="httpContext">
    /// The current HTTP context.
    /// </param>
    /// <returns>
    /// The correlation identifier from the request header when available;
    /// otherwise, the HTTP trace identifier.
    /// </returns>
    /// <remarks>
    /// This method first attempts to read the correlation identifier from
    /// <see cref="CustomHeader.CorrelationId"/>. If the header is not present
    /// or is empty, it falls back to <see cref="HttpContext.TraceIdentifier"/>.
    /// </remarks>
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
