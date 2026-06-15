using Microsoft.AspNetCore.Mvc;
using Muonroi.AuthZ.Authorization;
using Quickstart.AuthZ.Api.Models;

namespace Quickstart.AuthZ.Api.Controllers;

/// <summary>
/// Exercises Muonroi.AuthZ's primary public API: IAuthorizationPolicyEvaluator.
///
/// The evaluator runs every registered IRule&lt;AuthorizationRuleContext&gt;
/// (here ManagerOnlyDeleteRule) and returns an AuthorizationResult.
/// </summary>
[ApiController]
[Route("api/access")]
public class AccessController(IAuthorizationPolicyEvaluator evaluator) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // Evaluate an authorization request against the registered rules.
    // POST /api/access/check
    //
    // Try these bodies against ManagerOnlyDeleteRule:
    //   { "userId":"u1","tenantId":"t1","resource":"orders","action":"read","roles":[] }       → allowed
    //   { "userId":"u1","tenantId":"t1","resource":"orders","action":"delete","roles":[] }     → denied
    //   { "userId":"u1","tenantId":"t1","resource":"orders","action":"delete","roles":["manager"] } → allowed
    // ---------------------------------------------------------------------------
    [HttpPost("check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Check([FromBody] AccessCheckRequest request, CancellationToken token)
    {
        AuthorizationRuleContext context = new()
        {
            UserId = request.UserId,
            TenantId = request.TenantId,
            Resource = request.Resource,
            Action = request.Action,
            Roles = request.Roles
        };

        AuthorizationResult result = await evaluator.EvaluateAsync(context, token);

        if (!result.IsAuthorized)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                authorized = false,
                reason = result.DeniedReason
            });
        }

        return Ok(new { authorized = true });
    }
}
