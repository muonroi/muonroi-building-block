namespace Quickstart.Governance.Abstractions.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LicenseController : ControllerBase
{
    private readonly ILicenseGuard _licenseGuard;
    private readonly ILicenseStore _licenseStore;

    public LicenseController(ILicenseGuard licenseGuard, ILicenseStore licenseStore)
    {
        _licenseGuard = licenseGuard;
        _licenseStore = licenseStore;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Tier = _licenseGuard.Tier.ToString(),
            IsFreeMode = _licenseGuard.IsFreeMode,
            HasAdvancedFeature = _licenseGuard.HasFeature("AdvancedFeature")
        });
    }

    [HttpPost("action")]
    public IActionResult PerformAction()
    {
        // Demonstrate tier checks and feature gate pattern
        try
        {
            _licenseGuard.EnsureValid("WriteData");
            _licenseGuard.EnsureFeature("AdvancedFeature");

            return Ok("Action performed successfully under valid license with required features.");
        }
        catch (LicenseException ex)
        {
            return StatusCode(403, ex.Message);
        }
    }
    
    [HttpPost("offline-verification")]
    public IActionResult VerifyOffline([FromBody] LicensePayload payload)
    {
        // Demonstrate offline license payload handling
        _licenseStore.Save(payload);
        return Ok("Offline license saved to store.");
    }
}
