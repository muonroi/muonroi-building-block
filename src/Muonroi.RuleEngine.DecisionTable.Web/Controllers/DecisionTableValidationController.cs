namespace Muonroi.RuleEngine.DecisionTable.Web.Controllers;

/// <summary>
/// API endpoint for validating decision tables.
/// </summary>
[ApiController]
[Route("api/v1/decision-tables/{id}/validate")]
[Route("api/v1/rule-engine/decision-tables/{id}/validate")]
public sealed class DecisionTableValidationController(
    IDecisionTableStore store,
    DecisionTableValidator validator) : ControllerBase
{
    /// <summary>
    /// Validates a decision table by id.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
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
