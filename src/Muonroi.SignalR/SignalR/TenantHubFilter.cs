namespace Muonroi.SignalR.SignalR;

/// <summary>
/// Hub filter that sets <see cref="TenantContext.CurrentTenantId"/> for each invocation.
/// When multi-tenant is enabled, tenant id is required.
/// </summary>
public sealed class TenantHubFilter(ITenantIdResolver resolver, MTokenInfo tokenInfo, ILicenseGuard guard) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        HttpContext? httpContext = invocationContext.Context.GetHttpContext();
        string? tenantId = httpContext is not null
            ? await resolver.ResolveTenantIdAsync(httpContext)
            : null;

        if (tokenInfo.MultiTenantEnabled)
        {
            guard.EnsureFeature(FreeTierFeatures.Premium.MultiTenant);

            if (string.IsNullOrWhiteSpace(tenantId))
                throw new HubException("Tenant ID is required.");

            string? claimTenantId = invocationContext.Context.User?.FindFirst(ClaimConstants.TenantId)?.Value;
            if (!string.IsNullOrWhiteSpace(claimTenantId) &&
                !string.Equals(claimTenantId, tenantId, StringComparison.Ordinal))
                throw new HubException("Tenant mismatch.");
        }

        TenantContext.CurrentTenantId = tenantId;
        try
        {
            return await next(invocationContext);
        }
        finally
        {
            TenantContext.CurrentTenantId = null;
        }
    }
}
