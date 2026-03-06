namespace Muonroi.Tenancy.Core.Legacy;

public class TenantContextMiddleware(
    RequestDelegate next,
    ITenantIdResolver resolver,
    IMLogContext logContext,
    ITenantLicenseFeatureGate? licenseGuard = null,
    IOptions<MultiTenantConfigs>? options = null)
{
    private readonly bool _multiTenantEnabled = options?.Value.Enabled ?? false;
    private readonly bool _requireTenantClaimForAuthenticatedUser =
        options?.Value.RequireTenantClaimForAuthenticatedUser ?? true;

    public async Task Invoke(HttpContext context)
    {
        string? tenantId = (await resolver.ResolveTenantIdAsync(context))?.Trim();
        string? claimTenantId = context.User.FindFirst(ClaimConstants.TenantId)?.Value?.Trim();
        string? headerTenantId = context.Request.Headers.TryGetValue(CustomHeader.TenantId, out StringValues header)
            ? header.FirstOrDefault()?.Trim()
            : null;
        Endpoint? endpoint = context.GetEndpoint();
        bool isAllowAnonymous = endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() != null;

        // Endpoint metadata is not always available (for example when middleware runs before UseRouting).
        // In that case we still propagate tenant context but skip strict endpoint-aware enforcement.
        if (_multiTenantEnabled && endpoint is not null && !isAllowAnonymous)
        {
            if (licenseGuard is not null && !licenseGuard.HasFeature(TenantLicenseFeatures.Premium.MultiTenant))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            bool isAuthenticated = context.User.Identity?.IsAuthenticated == true;
            bool requireTenantClaim = _requireTenantClaimForAuthenticatedUser && isAuthenticated;
            if (!TenantSecurityValidator.TryValidate(
                    tenantId,
                    claimTenantId,
                    headerTenantId,
                    requireTenantClaim,
                    out _))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        try
        {
            TenantContext.CurrentTenantId = tenantId;
            using (logContext.PushProperty("TenantId", tenantId))
            {
                await next(context);
            }
        }
        finally
        {
            TenantContext.CurrentTenantId = null;
        }
    }
}
