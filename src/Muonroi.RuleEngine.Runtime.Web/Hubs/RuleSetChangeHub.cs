namespace Muonroi.RuleEngine.Runtime.Web.Hubs;

/// <summary>
/// SignalR hub that broadcasts runtime ruleset change events per tenant.
/// </summary>
public sealed class RuleSetChangeHub : Hub
{
    /// <summary>Joins the caller to a tenant-scoped ruleset change group.</summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <returns>A task representing the operation.</returns>
    public Task JoinTenantGroup(string tenantId)
    {
        string normalizedTenantId = NormalizeTenantId(tenantId);
        if (!CanJoinTenant(normalizedTenantId))
        {
            throw new HubException("Not authorized to subscribe to this tenant.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, BuildTenantGroup(normalizedTenantId));
    }

    /// <summary>Removes the caller from a tenant-scoped ruleset change group.</summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <returns>A task representing the operation.</returns>
    public Task LeaveTenantGroup(string tenantId)
    {
        string normalizedTenantId = NormalizeTenantId(tenantId);
        if (!CanJoinTenant(normalizedTenantId))
        {
            throw new HubException("Not authorized to unsubscribe from this tenant.");
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildTenantGroup(normalizedTenantId));
    }

    /// <summary>Builds the SignalR group name for a tenant.</summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <returns>Normalized group name.</returns>
    public static string BuildTenantGroup(string tenantId)
    {
        return $"tenant:{tenantId.Trim().ToLowerInvariant()}";
    }

    private bool CanJoinTenant(string tenantId)
    {
        ClaimsPrincipal? user = Context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("cp.admin") || user.IsInRole("cp.approver"))
        {
            return true;
        }

        bool hasPermissionClaim = user.Claims.Any(claim =>
            string.Equals(claim.Type, ClaimConstants.Permission, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(claim.Value, "cp.admin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Value, "cp.approver", StringComparison.OrdinalIgnoreCase)));
        if (hasPermissionClaim)
        {
            return true;
        }

        string? claimTenantId = user.FindFirst(ClaimConstants.TenantId)?.Value;
        return !string.IsNullOrWhiteSpace(claimTenantId) &&
               string.Equals(claimTenantId.Trim(), tenantId, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTenantId(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new HubException("tenantId is required.");
        }

        return tenantId.Trim();
    }
}
