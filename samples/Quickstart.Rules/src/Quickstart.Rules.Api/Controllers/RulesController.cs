using Microsoft.AspNetCore.Mvc;
using Muonroi.Rules.Feel;
using Muonroi.Rules.Flags;

namespace Quickstart.Rules.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RulesController : ControllerBase
{
    private readonly FeatureFlagEvaluator _featureFlags;

    public RulesController(FeatureFlagEvaluator featureFlags)
    {
        _featureFlags = featureFlags;
    }

    [HttpPost("feel/evaluate")]
    public IActionResult EvaluateFeel([FromBody] FeelRequest request)
    {
        var result = FeelEvaluator.EvaluateValue(request.Expression, request.Variables);
        return Ok(new { Result = result });
    }
    
    [HttpPost("flags/evaluate")]
    public IActionResult EvaluateFlag([FromBody] FlagRequest request)
    {
        // Example feature flag using FEEL under the hood
        var flagDefinition = new FeatureFlag
        {
            Id = request.FlagId,
            IsEnabled = true,
            Conditions = new List<FeatureFlagCondition>
            {
                new() { Expression = request.ConditionExpression }
            }
        };

        var isEnabled = _featureFlags.Evaluate(flagDefinition, request.Variables);
        return Ok(new { Flag = request.FlagId, IsEnabled = isEnabled });
    }
}

public class FeelRequest
{
    public string Expression { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
}

public class FlagRequest
{
    public string FlagId { get; set; } = string.Empty;
    public string ConditionExpression { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
}
