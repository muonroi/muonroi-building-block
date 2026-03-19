using Muonroi.RuleEngine.DecisionTable.Feel;
using Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

namespace Muonroi.RuleEngine.DecisionTable.Web.Controllers;

[ApiController]
[Route("api/v1/decision-tables/{id}/feel")]
public sealed class DecisionTableFeelController : ControllerBase
{
    [HttpPost("validate-expression")]
    public IActionResult ValidateExpression(
        string id,
        [FromServices] IFeelCellEvaluator evaluator,
        [FromBody] FeelValidateRequest request)
    {
        _ = id;
        string? error = evaluator.Validate(request.Expression, request.ColumnDataType);
        return Ok(new FeelValidateResponse
        {
            IsValid = string.IsNullOrWhiteSpace(error),
            Error = error
        });
    }
}
