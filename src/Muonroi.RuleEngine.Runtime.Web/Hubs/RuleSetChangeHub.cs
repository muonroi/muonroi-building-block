namespace Muonroi.RuleEngine.Runtime.Web.Hubs;

public sealed class RuleSetChangeHub : Hub
{
    public Task JoinTenantGroup(string tenantId)
    {
        string normalizedTenantId = NormalizeTenantId(tenantId);
        if (!CanJoinTenant(normalizedTenantId))
        {
            throw new HubException("Not authorized to subscribe to this tenant.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, BuildTenantGroup(normalizedTenantId));
    }

    public Task LeaveTenantGroup(string tenantId)
    {
        string normalizedTenantId = NormalizeTenantId(tenantId);
        if (!CanJoinTenant(normalizedTenantId))
        {
            throw new HubException("Not authorized to unsubscribe from this tenant.");
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildTenantGroup(normalizedTenantId));
    }

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
