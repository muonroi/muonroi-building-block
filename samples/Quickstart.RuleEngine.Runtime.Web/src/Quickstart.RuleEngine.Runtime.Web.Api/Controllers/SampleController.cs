using Microsoft.AspNetCore.Mvc;

namespace Quickstart.RuleEngine.Runtime.Web.Api.Controllers;

/// <summary>
/// Anonymous companion controller for the Runtime.Web sample.
///
/// The runtime governance controllers shipped by Muonroi.RuleEngine.Runtime.Web
/// (RuntimeRuleSetController at api/v1/rule-engine/rulesets, plus the rule-flow
/// contract / execute controllers) are decorated with [Authorize] and require a
/// configured authentication scheme. This controller exposes the surface that is
/// wired up so the sample is explorable without authentication.
/// </summary>
[ApiController]
[Route("api/runtime-web")]
public sealed class SampleController : ControllerBase
{
    /// <summary>
    /// Lists the governance endpoints registered by AddRuleEngineRuntimeWeb /
    /// MapRuleEngineRuntimeWeb so callers know what the package exposes.
    /// </summary>
    [HttpGet("endpoints")]
    public IActionResult ListEndpoints()
    {
        return Ok(new
        {
            description = "Endpoints registered by Muonroi.RuleEngine.Runtime.Web (all [Authorize]).",
            rulesets = new[]
            {
                "GET    api/v1/rule-engine/rulesets",
                "GET    api/v1/rule-engine/rulesets/{workflow}/versions",
                "GET    api/v1/rule-engine/rulesets/{workflow}/export",
                "POST   api/v1/rule-engine/rulesets/{workflow}",
                "POST   api/v1/rule-engine/rulesets/{workflow}/activate/{version}",
                "POST   api/v1/rule-engine/rulesets/{workflow}/validate",
                "POST   api/v1/rule-engine/rulesets/{workflow}/dry-run",
                "GET    api/v1/rule-engine/rulesets/{workflow}/audit"
            },
            hubs = new[] { "/hubs/ruleset-changes (SignalR)" },
            tracing = "Rule tracing endpoints (RuleTracing section)"
        });
    }
}
