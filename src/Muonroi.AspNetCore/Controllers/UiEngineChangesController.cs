using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Muonroi.AspNetCore.Extensions;
using Muonroi.AspNetCore.Models.Changes;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.Tenancy.Core;
using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.AspNetCore.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ui-engine/changes")]
public sealed class UiEngineChangesController(
    ICatalogScanService catalogScanService,
    IRuleChangeStore changeStore,
    IRuleChangeProposalStore proposalStore,
    IOptionsMonitor<RuleOptions> ruleOptionsMonitor,
    IAuthorizationService authorizationService,
    IAuthorizationPolicyProvider policyProvider,
    IMDateTimeService dateTimeService,
    IUiEngineSchemaNotifier? schemaNotifier,
    IMLog<UiEngineChangesController> logger) : ControllerBase
{
    private static readonly ActivitySource ActivitySource = new("Muonroi.UiEngine.Changes");

    [HttpPost("validate")]
    public async Task<ActionResult<RuleOrderChangeValidationResult>> Validate(
        [FromBody] RuleOrderChangeRequest request,
        CancellationToken cancellationToken)
    {
        (RuleOrderChangeValidationResult validation, _) = await ValidateInternalAsync(request, cancellationToken);
        return Ok(validation);
    }

    [HttpPost("apply")]
    public async Task<ActionResult<RuleChangeRecord>> Apply(
        [FromBody] RuleOrderChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!await EnsurePolicyAsync(UiEnginePolicyNames.Apply, cancellationToken))
        {
            return Forbid();
        }

        string tenantId = ResolveTenantId(request.TenantId);
        RuleOrderChangeRequest normalizedRequest = new()
        {
            EndpointRoute = NormalizeRoute(request.EndpointRoute),
            TenantId = tenantId,
            OrderedRuleCodes = [.. request.OrderedRuleCodes]
        };

        (RuleOrderChangeValidationResult validation, MUiEngineCatalogBinding? binding) =
            await ValidateInternalAsync(normalizedRequest, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        string appliedBy = ResolveAppliedBy();
        RuleChangeRecord record = await changeStore.ApplyAsync(normalizedRequest, appliedBy, cancellationToken);

        IReadOnlyList<string> availableRules = binding is null
            ? record.NewOrder
            : [.. binding.Rules.Select(x => x.Code)];
        ApplyTenantToggles(tenantId, availableRules, record.NewOrder);
        await NotifySchemaChangedAsync(cancellationToken);

        using Activity? activity = ActivitySource.StartActivity("ui_engine_changes.apply", ActivityKind.Internal);
        activity?.SetTag("tenant.id", tenantId);
        activity?.SetTag("endpoint.route", record.EndpointRoute);
        activity?.SetTag("change.id", record.ChangeId.ToString());
        activity?.SetTag("rule.count", record.NewOrder.Count);

        logger.Info(
            "Applied UI engine rule change {ChangeId} for tenant {TenantId} and route {Route} with {RuleCount} rules.",
            record.ChangeId,
            tenantId,
            record.EndpointRoute,
            record.NewOrder.Count);

        return Ok(record);
    }

    [HttpPost("propose")]
    public async Task<ActionResult<RuleChangeProposal>> Propose(
        [FromBody] ProposeRuleChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!await EnsurePolicyAsync(UiEnginePolicyNames.Propose, cancellationToken))
        {
            return Forbid();
        }

        string tenantId = ResolveTenantId(request.TenantId);
        RuleOrderChangeRequest validationRequest = new()
        {
            EndpointRoute = NormalizeRoute(request.EndpointRoute),
            TenantId = tenantId,
            OrderedRuleCodes = [.. request.OrderedRuleCodes]
        };

        (RuleOrderChangeValidationResult validation, _) = await ValidateInternalAsync(validationRequest, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        RuleChangeProposal proposal = await proposalStore.ProposeAsync(
            new ProposeRuleChangeRequest
            {
                EndpointRoute = validationRequest.EndpointRoute,
                TenantId = tenantId,
                OrderedRuleCodes = [.. validationRequest.OrderedRuleCodes],
                Note = request.Note
            },
            ResolveAppliedBy(),
            cancellationToken);

        using Activity? activity = ActivitySource.StartActivity("ui_engine_changes.propose", ActivityKind.Internal);
        activity?.SetTag("tenant.id", proposal.TenantId);
        activity?.SetTag("endpoint.route", proposal.EndpointRoute);
        activity?.SetTag("proposal.id", proposal.ProposalId.ToString());

        return Ok(proposal);
    }

    [HttpGet("proposals")]
    public async Task<ActionResult<IReadOnlyList<RuleChangeProposal>>> ListPendingProposals(
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        string resolvedTenant = ResolveTenantId(tenantId);
        IReadOnlyList<RuleChangeProposal> proposals = await proposalStore.ListPendingAsync(resolvedTenant, cancellationToken);
        return Ok(proposals);
    }

    [HttpPost("proposals/{proposalId:guid}/approve")]
    public async Task<ActionResult<RuleChangeApprovalResponse>> ApproveProposal(
        [FromRoute] Guid proposalId,
        [FromBody] ReviewProposalRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await EnsurePolicyAsync(UiEnginePolicyNames.Apply, cancellationToken))
        {
            return Forbid();
        }

        RuleChangeProposal? proposal = await proposalStore.GetAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return NotFound(new { message = "Proposal not found." });
        }

        if (proposal.Status != ProposalStatus.Pending)
        {
            return Conflict(new { message = $"Proposal is already {proposal.Status}." });
        }

        RuleOrderChangeRequest applyRequest = new()
        {
            EndpointRoute = proposal.EndpointRoute,
            TenantId = proposal.TenantId,
            OrderedRuleCodes = [.. proposal.OrderedRuleCodes]
        };

        (RuleOrderChangeValidationResult validation, MUiEngineCatalogBinding? binding) =
            await ValidateInternalAsync(applyRequest, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        string reviewer = ResolveAppliedBy();
        RuleChangeProposal? approved = await proposalStore.ApproveAsync(
            proposalId,
            reviewer,
            request?.ReviewNote,
            cancellationToken);
        if (approved is null)
        {
            return NotFound(new { message = "Proposal not found." });
        }

        RuleChangeRecord record = await changeStore.ApplyAsync(applyRequest, reviewer, cancellationToken);
        IReadOnlyList<string> availableRules = binding is null
            ? record.NewOrder
            : [.. binding.Rules.Select(x => x.Code)];
        ApplyTenantToggles(approved.TenantId, availableRules, record.NewOrder);
        await NotifySchemaChangedAsync(cancellationToken);

        using Activity? activity = ActivitySource.StartActivity("ui_engine_changes.approve", ActivityKind.Internal);
        activity?.SetTag("tenant.id", approved.TenantId);
        activity?.SetTag("endpoint.route", approved.EndpointRoute);
        activity?.SetTag("proposal.id", approved.ProposalId.ToString());
        activity?.SetTag("change.id", record.ChangeId.ToString());

        return Ok(new RuleChangeApprovalResponse
        {
            Proposal = approved,
            ChangeRecord = record
        });
    }

    [HttpPost("proposals/{proposalId:guid}/reject")]
    public async Task<ActionResult<RuleChangeProposal>> RejectProposal(
        [FromRoute] Guid proposalId,
        [FromBody] ReviewProposalRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await EnsurePolicyAsync(UiEnginePolicyNames.Apply, cancellationToken))
        {
            return Forbid();
        }

        RuleChangeProposal? proposal = await proposalStore.GetAsync(proposalId, cancellationToken);
        if (proposal is null)
        {
            return NotFound(new { message = "Proposal not found." });
        }

        if (proposal.Status != ProposalStatus.Pending)
        {
            return Conflict(new { message = $"Proposal is already {proposal.Status}." });
        }

        RuleChangeProposal? rejected = await proposalStore.RejectAsync(
            proposalId,
            ResolveAppliedBy(),
            request?.ReviewNote,
            cancellationToken);
        if (rejected is null)
        {
            return NotFound(new { message = "Proposal not found." });
        }

        using Activity? activity = ActivitySource.StartActivity("ui_engine_changes.reject", ActivityKind.Internal);
        activity?.SetTag("tenant.id", rejected.TenantId);
        activity?.SetTag("endpoint.route", rejected.EndpointRoute);
        activity?.SetTag("proposal.id", rejected.ProposalId.ToString());

        return Ok(rejected);
    }

    [HttpPost("rollback")]
    public async Task<ActionResult<RuleChangeRecord>> Rollback(
        [FromBody] RuleOrderRollbackRequest request,
        CancellationToken cancellationToken)
    {
        if (!await EnsurePolicyAsync(UiEnginePolicyNames.Apply, cancellationToken))
        {
            return Forbid();
        }

        string tenantId = ResolveTenantId(request.TenantId);
        string endpointRoute = NormalizeRoute(request.EndpointRoute);
        string appliedBy = ResolveAppliedBy();

        RuleChangeRecord? record = await changeStore.RollbackAsync(
            tenantId,
            endpointRoute,
            appliedBy,
            cancellationToken);
        if (record is null)
        {
            return NotFound(new
            {
                message = "No change history found for rollback."
            });
        }

        IReadOnlyList<string> currentOrder = await changeStore.GetCurrentAsync(tenantId, endpointRoute, cancellationToken);
        HashSet<string> allKnownCodes = new(StringComparer.OrdinalIgnoreCase);
        foreach (string code in record.PreviousOrder)
        {
            _ = allKnownCodes.Add(code);
        }

        foreach (string code in record.NewOrder)
        {
            _ = allKnownCodes.Add(code);
        }

        foreach (string code in currentOrder)
        {
            _ = allKnownCodes.Add(code);
        }

        ApplyTenantToggles(tenantId, [.. allKnownCodes], currentOrder);
        await NotifySchemaChangedAsync(cancellationToken);

        using Activity? activity = ActivitySource.StartActivity("ui_engine_changes.rollback", ActivityKind.Internal);
        activity?.SetTag("tenant.id", tenantId);
        activity?.SetTag("endpoint.route", endpointRoute);
        activity?.SetTag("change.id", record.ChangeId.ToString());

        logger.Info(
            "Rolled back UI engine rule change for tenant {TenantId} and route {Route}.",
            tenantId,
            endpointRoute);

        return Ok(record);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<RuleChangeRecord>>> History(
        [FromQuery] string endpointRoute,
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        string normalizedTenantId = ResolveTenantId(tenantId);
        string normalizedRoute = NormalizeRoute(endpointRoute);
        IReadOnlyList<RuleChangeRecord> history = await changeStore.GetHistoryAsync(
            normalizedTenantId,
            normalizedRoute,
            cancellationToken);
        return Ok(history);
    }

    private async Task<(RuleOrderChangeValidationResult Validation, MUiEngineCatalogBinding? Binding)> ValidateInternalAsync(
        RuleOrderChangeRequest request,
        CancellationToken cancellationToken)
    {
        RuleOrderChangeValidationResult result = new();
        string route = NormalizeRoute(request.EndpointRoute);
        string tenantId = ResolveTenantId(request.TenantId);
        List<string> orderedCodes = request.OrderedRuleCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        if (orderedCodes.Count == 0)
        {
            result.Errors.Add("orderedRuleCodes is required.");
        }

        if (orderedCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != orderedCodes.Count)
        {
            result.Errors.Add("orderedRuleCodes has duplicates.");
        }

        IReadOnlyList<MUiEngineCatalogBinding> bindings = await catalogScanService.BuildBindingsAsync(cancellationToken);
        MUiEngineCatalogBinding? binding = bindings.FirstOrDefault(x =>
            string.Equals(NormalizeRoute(x.EndpointRoute), route, StringComparison.OrdinalIgnoreCase));
        if (binding is null)
        {
            result.Errors.Add($"No catalog binding found for endpoint route '{route}'.");
            result.IsValid = false;
            return (result, null);
        }

        HashSet<string> availableCodes = [.. binding.Rules.Select(x => x.Code)];
        foreach (string code in orderedCodes)
        {
            if (!availableCodes.Contains(code))
            {
                result.Errors.Add($"Rule code '{code}' is not available for route '{route}'.");
            }
        }

        IReadOnlyList<MUiEngineCatalogRuleDescriptor> allRules = await catalogScanService.ScanRulesAsync(cancellationToken);
        Dictionary<string, MUiEngineCatalogRuleDescriptor> byCode = allRules
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> indexByCode = orderedCodes
            .Select((code, idx) => (code, idx))
            .ToDictionary(x => x.code, x => x.idx, StringComparer.OrdinalIgnoreCase);

        foreach (string code in orderedCodes)
        {
            if (!byCode.TryGetValue(code, out MUiEngineCatalogRuleDescriptor? descriptor))
            {
                continue;
            }

            foreach (string dependency in descriptor.DependsOn)
            {
                if (!indexByCode.TryGetValue(dependency, out int dependencyIndex))
                {
                    result.Errors.Add($"Rule '{code}' requires dependency '{dependency}' in orderedRuleCodes.");
                    continue;
                }

                if (dependencyIndex > indexByCode[code])
                {
                    result.Errors.Add($"Rule '{code}' must be placed after dependency '{dependency}'.");
                }
            }
        }

        if (HasDependencyCycle(orderedCodes, byCode))
        {
            result.Errors.Add("orderedRuleCodes introduces a circular dependency.");
        }

        foreach (string available in availableCodes)
        {
            if (!indexByCode.ContainsKey(available))
            {
                result.Warnings.Add($"Rule '{available}' is omitted and will be disabled for tenant '{tenantId}'.");
            }
        }

        result.IsValid = result.Errors.Count == 0;
        return (result, binding);
    }

    private void ApplyTenantToggles(string tenantId, IReadOnlyList<string> availableRuleCodes, IReadOnlyList<string> enabledRuleCodes)
    {
        RuleOptions options = ruleOptionsMonitor.CurrentValue;
        if (!options.TenantRuleToggles.TryGetValue(tenantId, out Dictionary<string, bool>? tenantMap))
        {
            tenantMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            options.TenantRuleToggles[tenantId] = tenantMap;
        }

        HashSet<string> enabledSet = [.. enabledRuleCodes];
        foreach (string code in availableRuleCodes)
        {
            tenantMap[code] = enabledSet.Contains(code);
        }
    }

    private static bool HasDependencyCycle(
        IReadOnlyList<string> orderedCodes,
        IReadOnlyDictionary<string, MUiEngineCatalogRuleDescriptor> byCode)
    {
        HashSet<string> selected = [.. orderedCodes];
        HashSet<string> visiting = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);

        bool Visit(string code)
        {
            if (visited.Contains(code))
            {
                return false;
            }

            if (!visiting.Add(code))
            {
                return true;
            }

            if (byCode.TryGetValue(code, out MUiEngineCatalogRuleDescriptor? rule))
            {
                foreach (string dependency in rule.DependsOn)
                {
                    if (!selected.Contains(dependency))
                    {
                        continue;
                    }

                    if (Visit(dependency))
                    {
                        return true;
                    }
                }
            }

            _ = visiting.Remove(code);
            _ = visited.Add(code);
            return false;
        }

        foreach (string code in orderedCodes)
        {
            if (Visit(code))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> EnsurePolicyAsync(string policyName, CancellationToken cancellationToken)
    {
        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(policyName);
        if (policy is null)
        {
            return true;
        }

        AuthorizationResult authorization = await authorizationService.AuthorizeAsync(User, null, policyName);
        return authorization.Succeeded;
    }

    private async Task NotifySchemaChangedAsync(CancellationToken cancellationToken)
    {
        if (schemaNotifier is null)
        {
            return;
        }

        MUiEngineSchemaVersion schemaVersion = new()
        {
            Version = MUiEngineManifest.MSchemaVersionV2,
            SchemaHash = Guid.NewGuid().ToString("N"),
            GeneratedAtUtc = dateTimeService.UtcNow()
        };

        await schemaNotifier.NotifySchemaChangedAsync(schemaVersion, cancellationToken);
    }

    private string ResolveAppliedBy()
    {
        return User.FindFirst(ClaimConstants.UserIdentifier)?.Value
               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.Identity?.Name
               ?? "system";
    }

    private static string ResolveTenantId(string? tenantId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            return tenantId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(TenantContext.CurrentTenantId))
        {
            return TenantContext.CurrentTenantId;
        }

        return "_global";
    }

    private static string NormalizeRoute(string? endpointRoute)
    {
        if (string.IsNullOrWhiteSpace(endpointRoute))
        {
            return "/";
        }

        string route = endpointRoute.Trim();
        if (!route.StartsWith('/'))
        {
            route = "/" + route;
        }

        while (route.Contains("//", StringComparison.Ordinal))
        {
            route = route.Replace("//", "/", StringComparison.Ordinal);
        }

        return route;
    }
}

public sealed class RuleChangeApprovalResponse
{
    public RuleChangeProposal Proposal { get; set; } = new();
    public RuleChangeRecord ChangeRecord { get; set; } = new();
}
