namespace Quickstart.RuleEngine.EntityFrameworkCore.Api.Controllers;

/// <summary>
/// Demonstrates the RulesEngineService backed by PostgresRuleSetStore.
///
/// Every endpoint here requires a reachable Postgres instance (configured via
/// the RuleDb connection string). They show the primary public API of the
/// EF Core control plane: list workflows, export a ruleset, validate, and save.
/// </summary>
[ApiController]
[Route("api/rulesets")]
public sealed class RuleSetsController(RulesEngineService service) : ControllerBase
{
    /// <summary>Lists all workflows known to the Postgres-backed store.</summary>
    [HttpGet("workflows")]
    public async Task<IActionResult> ListWorkflows(CancellationToken ct)
    {
        IReadOnlyList<string> workflows = await service.GetWorkflowsAsync(ct);
        return Ok(workflows);
    }

    /// <summary>Returns the version numbers stored for a workflow.</summary>
    [HttpGet("{workflow}/versions")]
    public async Task<IActionResult> GetVersions(string workflow, CancellationToken ct)
    {
        int[] versions = await service.GetVersionsAsync(workflow, ct);
        int? active = await service.GetActiveVersionAsync(workflow, ct);
        return Ok(new { workflow, active, versions });
    }

    /// <summary>Exports the active (or specified) ruleset JSON for a workflow.</summary>
    [HttpGet("{workflow}/export")]
    public async Task<IActionResult> Export(string workflow, [FromQuery] int? version, CancellationToken ct)
    {
        string? json = await service.GetRuleSetAsync(workflow, version, ct);
        return json is null
            ? NotFound(new { message = $"No ruleset found for workflow '{workflow}'." })
            : Content(json, "application/json");
    }

    /// <summary>Validates a ruleset definition without persisting it.</summary>
    [HttpPost("{workflow}/validate")]
    public async Task<IActionResult> Validate(string workflow, [FromBody] string ruleSetJson, CancellationToken ct)
    {
        RuleSetValidationResult result = await service.ValidateRuleSetAsync(workflow, ruleSetJson, ct);
        return Ok(result);
    }

    /// <summary>Saves a ruleset definition for a workflow (new version).</summary>
    [HttpPost("{workflow}")]
    public async Task<IActionResult> Save(string workflow, [FromBody] string ruleSetJson, CancellationToken ct)
    {
        RuleSetValidationResult validation = await service.ValidateRuleSetAsync(workflow, ruleSetJson, ct);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        await service.SaveRuleSetAsync(workflow, ruleSetJson, ct);
        int[] versions = await service.GetVersionsAsync(workflow, ct);
        return Ok(new { workflow, savedVersion = versions.Length == 0 ? 1 : versions.Max() });
    }
}
