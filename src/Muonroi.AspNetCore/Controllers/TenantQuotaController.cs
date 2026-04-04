namespace Muonroi.AspNetCore.Controllers;

/// <inheritdoc />
[ApiController]
[Route("api/v1/tenants/{tenantId}/quotas")]
[Authorize]
public sealed class TenantQuotaController(
    ITenantQuotaTracker quotaTracker,
    ITenantQuotaStore quotaStore) : ControllerBase
{
/// <inheritdoc />
    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(string tenantId, CancellationToken ct = default)
    {
        if (!CanAccessTenant(tenantId))
        {
            return Forbid();
        }

        QuotaUsage usage = await quotaTracker.GetUsageAsync(tenantId, ct);
        return Ok(usage);
    }

/// <inheritdoc />
    [HttpGet("limits")]
    public async Task<IActionResult> GetLimits(string tenantId, CancellationToken ct = default)
    {
        if (!CanAccessTenant(tenantId))
        {
            return Forbid();
        }

        TenantQuota quota = await quotaStore.GetQuotaAsync(tenantId, ct) ?? TenantQuotaPresets.Free;
        quota.TenantId = tenantId;
        return Ok(quota);
    }

/// <inheritdoc />
    [HttpPut("limits")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateLimits(string tenantId, [FromBody] TenantQuota quota, CancellationToken ct = default)
    {
        quota.TenantId = tenantId;
        await quotaStore.SaveQuotaAsync(tenantId, quota, ct);
        return Ok(quota);
    }

/// <inheritdoc />
    [HttpPost("upgrade")]
    public async Task<IActionResult> UpgradeTier(string tenantId, [FromBody] UpgradeRequest request, CancellationToken ct = default)
    {
        if (!CanAccessTenant(tenantId))
        {
            return Forbid();
        }

        TenantQuota newQuota = request.Tier switch
        {
            TenantTier.Starter => TenantQuotaPresets.Starter,
            TenantTier.Professional => TenantQuotaPresets.Professional,
            TenantTier.Enterprise => TenantQuotaPresets.Enterprise,
            _ => TenantQuotaPresets.Free
        };

        newQuota.TenantId = tenantId;
        await quotaStore.SaveQuotaAsync(tenantId, newQuota, ct);
        return Ok(new
        {
            message = $"Upgraded tenant '{tenantId}' to tier '{request.Tier}'.",
            quota = newQuota
        });
    }

    private static bool CanAccessTenant(string tenantId)
    {
        return string.Equals(TenantContext.CurrentTenantId, tenantId, StringComparison.OrdinalIgnoreCase);
    }
}

/// <inheritdoc />
public sealed record UpgradeRequest(TenantTier Tier);
