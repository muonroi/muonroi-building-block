namespace Muonroi.SignalR.SignalR;

/// <summary>
/// Hub filter that sets <see cref="TenantContext.CurrentTenantId"/> for each invocation.
/// When multi-tenant is enabled, tenant id is required.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0001", Justification = "SignalR boundary: HubException message is the contract surfaced to clients.")]
public sealed class TenantHubFilter(ITenantIdResolver resolver, MTokenInfo tokenInfo, ILicenseGuard guard) : IHubFilter
{
    /// <summary>
    /// Resolves tenant id for the invocation and enforces tenant requirements.
    /// </summary>
    /// <param name="invocationContext">Hub invocation context.</param>
    /// <param name="next">Next filter in the pipeline.</param>
    /// <returns>The invocation result.</returns>
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
