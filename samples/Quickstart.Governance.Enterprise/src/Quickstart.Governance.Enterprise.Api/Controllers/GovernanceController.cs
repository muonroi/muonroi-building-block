using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Governance.License;
using Muonroi.Governance.Operations;

namespace Quickstart.Governance.Enterprise.Api.Controllers;

/// <summary>
/// Exercises Muonroi.Governance.Enterprise's public surface:
///   - ILicenseGuard (from the OSS layer, upgraded with the enterprise enhancer)
///   - IMEnterpriseSloPresetService (enterprise operations)
///
/// Both are registered by AddMEnterpriseGovernance(). With no license file the
/// app runs in Free tier, so premium feature checks are denied.
/// </summary>
[ApiController]
[Route("api/governance")]
public class GovernanceController(
    ILicenseGuard guard,
    IMEnterpriseSloPresetService sloPresets) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // 1. License status (guard upgraded with EnterpriseLicenseGuardEnhancer)
    //    GET /api/governance/status
    // ---------------------------------------------------------------------------
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            tier = guard.Tier.ToString(),
            isFreeMode = guard.IsFreeMode,
            isValid = guard.Current.IsValid
        });
    }

    // ---------------------------------------------------------------------------
    // 2. Enterprise feature enforcement
    //    POST /api/governance/feature/{featureName}/ensure
    //
    //    EnsureFeature throws when the feature is not licensed. Try "anti-tampering"
    //    or "audit-trail" — denied in Free tier, allowed under an Enterprise license.
    // ---------------------------------------------------------------------------
    [HttpPost("feature/{featureName}/ensure")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    public IActionResult EnsureFeature(string featureName)
    {
        try
        {
            guard.EnsureFeature(featureName);
            return Ok(new { featureName, granted = true });
        }
        catch (MException ex)
        {
            // No silent catch — surface the license denial reason.
            return StatusCode(StatusCodes.Status402PaymentRequired, new
            {
                featureName,
                granted = false,
                reason = ex.Message
            });
        }
    }

    // ---------------------------------------------------------------------------
    // 3. Enterprise SLO presets (IMEnterpriseSloPresetService)
    //    GET /api/governance/slo-presets
    //    GET /api/governance/slo-presets/{name}
    // ---------------------------------------------------------------------------
    [HttpGet("slo-presets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ListSloPresets()
    {
        return Ok(sloPresets.GetPresetNames());
    }

    [HttpGet("slo-presets/{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetSloPreset(string name)
    {
        try
        {
            return Ok(sloPresets.GetPreset(name));
        }
        catch (MException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
