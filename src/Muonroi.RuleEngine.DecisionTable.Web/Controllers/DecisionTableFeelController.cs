using Muonroi.RuleEngine.DecisionTable.Feel;
using Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

namespace Muonroi.RuleEngine.DecisionTable.Web.Controllers;

/// <summary>
/// API endpoints for FEEL validation related to decision tables.
/// </summary>
[ApiController]
[Route("api/v1/decision-tables/{id}/feel")]
[Route("api/v1/rule-engine/decision-tables/{id}/feel")]
public sealed class DecisionTableFeelController : ControllerBase
{
    /// <summary>
    /// Validates a FEEL expression against the provided column data type.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="evaluator">FEEL expression evaluator.</param>
    /// <param name="request">Validation request.</param>
    /// <returns>Validation response.</returns>
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
