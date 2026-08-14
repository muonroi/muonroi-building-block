namespace Quickstart.Governance.Api.Controllers;

/// <summary>
/// Exercises Muonroi.Governance's primary public API: ILicenseGuard.
///
/// ILicenseGuard is registered by AddLicenseProtection(). With no license file
/// present the app runs in Free tier, so feature checks for premium capabilities
/// fail while free-tier capabilities pass.
/// </summary>
[ApiController]
[Route("api/license")]
public class LicenseController(ILicenseGuard guard) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // 1. Current license status
    //    GET /api/license/status
    //
    //    Reads ILicenseGuard.Tier / IsFreeMode and the resolved LicenseState.
    // ---------------------------------------------------------------------------
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        LicenseState state = guard.Current;
        return Ok(new
        {
            tier = guard.Tier.ToString(),
            isFreeMode = guard.IsFreeMode,
            isValid = state.IsValid,
            isExpired = state.IsExpired,
            organization = state.OrganizationName,
            features = state.Features
        });
    }

    // ---------------------------------------------------------------------------
    // 2. Feature availability probe (non-throwing)
    //    GET /api/license/feature/{featureName}
    //
    //    HasFeature returns whether the capability is allowed under the current
    //    tier. Try "db.query" (free → true) vs "multi-tenant" (premium → false).
    // ---------------------------------------------------------------------------
    [HttpGet("feature/{featureName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult HasFeature(string featureName)
    {
        return Ok(new { featureName, available = guard.HasFeature(featureName) });
    }

    // ---------------------------------------------------------------------------
    // 3. Feature enforcement (throwing)
    //    POST /api/license/feature/{featureName}/ensure
    //
    //    EnsureFeature throws LicenseException when the feature is not licensed.
    //    In Free tier, premium features (e.g. "rule-engine") are rejected.
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
            // No silent catch — surface the license denial reason to the caller.
            return StatusCode(StatusCodes.Status402PaymentRequired, new
            {
                featureName,
                granted = false,
                reason = ex.Message
            });
        }
    }

    // ---------------------------------------------------------------------------
    // 4. Action enforcement
    //    POST /api/license/action/{actionType}
    //
    //    EnsureValid validates the license for an action type. Free-tier actions
    //    such as "db.query" pass; premium action types are rejected in Free tier.
    // ---------------------------------------------------------------------------
    [HttpPost("action/{actionType}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    public IActionResult EnsureValid(string actionType)
    {
        try
        {
            guard.EnsureValid(actionType);
            return Ok(new { actionType, allowed = true });
        }
        catch (MException ex)
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, new
            {
                actionType,
                allowed = false,
                reason = ex.Message
            });
        }
    }
}
