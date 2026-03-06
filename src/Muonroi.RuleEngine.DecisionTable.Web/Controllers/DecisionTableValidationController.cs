namespace Muonroi.RuleEngine.DecisionTable.Web.Controllers;

[ApiController]
[Route("api/v1/decision-tables/{id}/validate")]
public sealed class DecisionTableValidationController(
    IDecisionTableStore store,
    DecisionTableValidator validator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Validate(string id, CancellationToken cancellationToken = default)
    {
        DecisionTableModel? table = await store.GetByIdAsync(id, cancellationToken);
        if (table is null)
        {
            return NotFound();
        }

        ValidationResult result = validator.Validate(table);
        return Ok(result);
    }
}
