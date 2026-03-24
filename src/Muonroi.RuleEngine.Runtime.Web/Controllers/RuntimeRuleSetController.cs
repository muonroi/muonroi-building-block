using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Runtime.Web.ViewModels;

namespace Muonroi.RuleEngine.Runtime.Web.Controllers;

/// <summary>
/// API endpoints for listing, exporting, validating, activating, and auditing runtime rulesets.
/// </summary>
/// <param name="service">Rules engine service for ruleset operations.</param>
/// <param name="auditStore">Audit store for ruleset change history.</param>
/// <param name="executionContextAccessor">Execution context accessor for tenant resolution.</param>
[ApiController]
[Authorize]
[Route("api/v1/rule-engine/rulesets")]
public sealed class RuntimeRuleSetController(
    RulesEngineService service,
    IRuleSetAuditStore auditStore,
    ISystemExecutionContextAccessor executionContextAccessor) : ControllerBase
{
    /// <summary>Lists all workflows with version summaries.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of workflow summaries.</returns>
    [HttpGet]
    public async Task<IActionResult> ListWorkflows(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> workflows = await service.GetWorkflowsAsync(cancellationToken);
        List<RuleSetWorkflowSummary> items = [];
        foreach (string workflow in workflows)
        {
            int[] versions = await service.GetVersionsAsync(workflow, cancellationToken);
            int? activeVersion = await service.GetActiveVersionAsync(workflow, cancellationToken);
            items.Add(new RuleSetWorkflowSummary
            {
                WorkflowName = workflow,
                ActiveVersion = activeVersion,
                Versions = versions
            });
        }

        return Ok(items);
    }

    /// <summary>Gets rich version details for a workflow, compatible with MVersionDropdown.</summary>
    /// <param name="workflow">Workflow identifier.</param>
    /// <param name="limit">Page size (default 10).</param>
    /// <param name="offset">Number of items to skip (default 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of version items with status, isActive, and createdAt.</returns>
    [HttpGet("{workflow}/versions")]
    public async Task<IActionResult> GetVersions(string workflow, [FromQuery] int limit = 10, [FromQuery] int offset = 0, CancellationToken cancellationToken = default)
    {
        var details = await service.GetVersionDetailsAsync(workflow, limit, offset, cancellationToken);
        var items = details.Select(d => new RuleSetVersionItem
        {
            Version = d.Version,
            Status = d.Status,
            IsActive = d.IsActive,
            CreatedAt = d.CreatedAt
        }).ToArray();
        return Ok(items);
    }

    /// <summary>Exports a ruleset JSON payload for a workflow.</summary>
    /// <param name="workflow">Workflow identifier.</param>
    /// <param name="version">Optional ruleset version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ruleset export payload.</returns>
    [HttpGet("{workflow}/export")]
    public async Task<IActionResult> Export(
        string workflow,
        [FromQuery] int? version = null,
        CancellationToken cancellationToken = default)
    {
        string? json = await service.GetRuleSetAsync(workflow, version, cancellationToken);
        if (json is null)
        {
            return NotFound(new { message = $"Ruleset not found for workflow '{workflow}'." });
        }

        RuleSetValidationResult validation = await service.ValidateRuleSetAsync(workflow, json, cancellationToken);
        RuleSetExportResponse response = new()
        {
            WorkflowName = workflow,
            Version = version ?? await service.GetActiveVersionAsync(workflow, cancellationToken),
            RuleSetJson = json,
            ValidationShape = validation.Shape
        };
        return Ok(response);
    }

    /// <summary>Saves a ruleset definition for a workflow.</summary>
    /// <param name="workflow">Workflow identifier.</param>
    /// <param name="request">Ruleset save request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Save result payload.</returns>
    [HttpPost("{workflow}")]
    public async Task<IActionResult> Save(
        string workflow,
        [FromBody] SaveRuleSetRequest request,
        CancellationToken cancellationToken = default)
    {
        string json = EnsureJsonPayload(request.RuleSet, "ruleSet");
        RuleSetValidationResult validation = await service.ValidateRuleSetAsync(workflow, json, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        int? previousActiveVersion = await service.GetActiveVersionAsync(workflow, cancellationToken);
        await service.SaveRuleSetAsync(workflow, json, cancellationToken);
        int[] versions = await service.GetVersionsAsync(workflow, cancellationToken);
        int savedVersion = versions.Length == 0 ? 1 : versions.Max();
        bool activated = true;

        if (!request.ActivateAfterSave && previousActiveVersion.HasValue)
        {
            await service.SetActiveVersionAsync(workflow, previousActiveVersion.Value, cancellationToken);
            activated = false;
        }

        int? activeVersion = await service.GetActiveVersionAsync(workflow, cancellationToken);
        await WriteAuditAsync(
            workflow,
            "Save",
            savedVersion,
            ResolveActor(request.Actor),
            request.Detail,
            cancellationToken);

        RuleSetSaveResponse response = new()
        {
            WorkflowName = workflow,
            SavedVersion = savedVersion,
            ActiveVersion = activeVersion,
            Activated = activated
        };
        return Ok(response);
    }

    /// <summary>Activates a specific ruleset version for a workflow.</summary>
    /// <param name="workflow">Workflow identifier.</param>
    /// <param name="version">Ruleset version to activate.</param>
    /// <param name="request">Optional activation request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Workflow summary after activation.</returns>
    [HttpPost("{workflow}/activate/{version:int}")]
    [HttpPost("{workflow}/{version:int}/activate")]
    public async Task<IActionResult> Activate(
        string workflow,
        int version,
        [FromBody] ActivateRuleSetRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        await service.SetActiveVersionAsync(workflow, version, cancellationToken);
        await WriteAuditAsync(
            workflow,
            "Activate",
            version,
            ResolveActor(request?.Actor),
            request?.Detail,
            cancellationToken);

        int[] versions = await service.GetVersionsAsync(workflow, cancellationToken);
        RuleSetWorkflowSummary response = new()
        {
            WorkflowName = workflow,
            ActiveVersion = await service.GetActiveVersionAsync(workflow, cancellationToken),
            Versions = versions
        };
        return Ok(response);
    }

    /// <summary>Validates a ruleset definition without saving it.</summary>
    /// <param name="workflow">Workflow identifier.</param>
    /// <param name="request">Validation request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    [HttpPost("{workflow}/validate")]
    public async Task<IActionResult> Validate(
        string workflow,
        [FromBody] ValidateRuleSetRequest request,
        CancellationToken cancellationToken = default)
    {
        string json = EnsureJsonPayload(request.RuleSet, "ruleSet");
        RuleSetValidationResult result = await service.ValidateRuleSetAsync(workflow, json, cancellationToken);
        return Ok(result);
    }

    /// <summary>Runs a ruleset in dry-run mode for a workflow.</summary>
    /// <param name="workflow">Workflow identifier.</param>
    /// <param name="request">Dry-run request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dry-run result payload.</returns>
    [HttpPost("{workflow}/dry-run")]
    public async Task<IActionResult> DryRun(
        string workflow,
        [FromBody] DryRunRuleSetRequest request,
        CancellationToken cancellationToken = default)
    {
        string json = EnsureJsonPayload(request.RuleSet, "ruleSet");
        RuleSetValidationResult validation = await service.ValidateRuleSetAsync(workflow, json, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        try
        {
            FactBag facts = await service.DryRunAsync(workflow, json, request.Context, request.ContextType, cancellationToken);
            await WriteAuditAsync(workflow, "DryRun", null, ResolveActor(), null, cancellationToken);
            RuleSetDryRunResponse response = new()
            {
                IsSuccess = true,
                Facts = new Dictionary<string, object?>(facts.AsReadOnly(), StringComparer.OrdinalIgnoreCase),
                Errors = []
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            RuleSetDryRunResponse response = new()
            {
                IsSuccess = false,
                Facts = new Dictionary<string, object?>(),
                Errors = [ex.Message]
            };
            return BadRequest(response);
        }
    }

    /// <summary>Returns audit history for a workflow.</summary>
    /// <param name="workflow">Workflow identifier.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audit page payload.</returns>
    [HttpGet("{workflow}/audit")]
    public async Task<IActionResult> Audit(
        string workflow,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        RuleSetAuditPage result = await auditStore.QueryAsync(workflow, page, pageSize, cancellationToken);
        return Ok(result);
    }

    private async Task WriteAuditAsync(
        string workflow,
        string action,
        int? version,
        string? actor,
        string? detail,
        CancellationToken cancellationToken)
    {
        RuleSetAuditEntry entry = new()
        {
            WorkflowName = workflow,
            Action = action,
            Version = version,
            Actor = actor,
            Detail = detail,
            TenantId = string.IsNullOrWhiteSpace(executionContextAccessor.Get().TenantId)
                ? "default"
                : executionContextAccessor.Get().TenantId!
        };
        await auditStore.AppendAsync(entry, cancellationToken);
    }

    private string? ResolveActor(string? actor = null)
    {
        if (!string.IsNullOrWhiteSpace(actor))
        {
            return actor;
        }

        string? identityName = User?.FindFirstValue(ClaimTypes.Name) ?? User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(identityName))
        {
            return identityName;
        }

        if (Request.Headers.TryGetValue("X-Actor", out Microsoft.Extensions.Primitives.StringValues actorHeader))
        {
            string? headerActor = actorHeader.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerActor))
            {
                return headerActor;
            }
        }

        return null;
    }

    private static string EnsureJsonPayload(JsonElement payload, string fieldName)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new InvalidDataException($"Request field '{fieldName}' is required.");
        }

        return payload.GetRawText();
    }
}
