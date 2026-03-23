using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Web.Controllers;
using Muonroi.RuleEngine.Runtime.Web.ViewModels;
using System.Text.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuntimeRuleSetControllerTests
{
    [Fact]
    public async Task Save_Then_Export_ShouldReturnPersistedRuleset()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            SystemExecutionContextAccessor accessor = new();
            FileRuleSetStore store = new(root, executionContextAccessor: accessor);
            RulesEngineService service = new(store, executionContextAccessor: accessor);
            FileRuleSetAuditStore auditStore = new(root, new MJsonSerializeService(), accessor);

            RuntimeRuleSetController controller = CreateController(service, auditStore, accessor);
            controller.ControllerContext.HttpContext.Request.Headers["X-Actor"] = "runtime-test";

            SaveRuleSetRequest saveRequest = new()
            {
                RuleSet = ParseJson("""{"workflowName":"wf-api","rules":["RULE_A"]}"""),
                ActivateAfterSave = true
            };

            SetTenant(accessor, "tenant-api");
            IActionResult saveResult = await controller.Save("wf-api", saveRequest);
            OkObjectResult saveOk = saveResult.Should().BeOfType<OkObjectResult>().Subject;
            RuleSetSaveResponse savePayload = saveOk.Value.Should().BeOfType<RuleSetSaveResponse>().Subject;
            savePayload.SavedVersion.Should().Be(1);
            savePayload.ActiveVersion.Should().Be(1);

            IActionResult exportResult = await controller.Export("wf-api");
            OkObjectResult exportOk = exportResult.Should().BeOfType<OkObjectResult>().Subject;
            RuleSetExportResponse exportPayload = exportOk.Value.Should().BeOfType<RuleSetExportResponse>().Subject;
            exportPayload.RuleSetJson.Should().Contain("RULE_A");

            RuleSetAuditPage auditPage = await auditStore.QueryAsync("wf-api");
            auditPage.TotalCount.Should().BeGreaterThanOrEqualTo(1);
            accessor.Clear();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Validate_ShouldReturnErrors_WhenWorkflowMismatched()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        RulesEngineService service = new(new FileRuleSetStore(root));
        SystemExecutionContextAccessor accessor = new();
        RuntimeRuleSetController controller = new(
            service,
            new FileRuleSetAuditStore(root, new MJsonSerializeService(), accessor),
            accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        const string ruleSetText = """
                                   {
                                     "workflowName": "wf-different",
                                     "rules": [ "RULE_A" ]
                                   }
                                   """;
        ValidateRuleSetRequest request = new()
        {
            RuleSet = JsonDocument.Parse(ruleSetText).RootElement.Clone()
        };

        IActionResult result = await controller.Validate("wf-expected", request);
        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        RuleSetValidationResult payload = ok.Value.Should().BeOfType<RuleSetValidationResult>().Subject;

        payload.IsValid.Should().BeFalse();
        payload.Errors.Should().Contain(x => x.Code == "WorkflowMismatch");
    }

    [Fact]
    public async Task ListWorkflows_And_GetVersions_ShouldReturnVersionSummaries()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            SystemExecutionContextAccessor accessor = new();
            FileRuleSetStore store = new(root, executionContextAccessor: accessor);
            RulesEngineService service = new(store, executionContextAccessor: accessor);
            RuntimeRuleSetController controller = CreateController(
                service,
                new FileRuleSetAuditStore(root, new MJsonSerializeService(), accessor),
                accessor);

            await service.SaveRuleSetAsync("wf-a", """{"workflowName":"wf-a","rules":["RULE_A"]}""");
            await service.SetActiveVersionAsync("wf-a", 1);
            await service.SaveRuleSetAsync("wf-b", """{"workflowName":"wf-b","rules":["RULE_B"]}""");
            await service.SaveRuleSetAsync("wf-b", """{"workflowName":"wf-b","rules":["RULE_C"]}""");
            await service.SetActiveVersionAsync("wf-b", 2);

            IActionResult listResult = await controller.ListWorkflows();
            OkObjectResult listOk = listResult.Should().BeOfType<OkObjectResult>().Subject;
            IReadOnlyList<RuleSetWorkflowSummary> listPayload = listOk.Value.Should().BeAssignableTo<IReadOnlyList<RuleSetWorkflowSummary>>().Subject;
            listPayload.Should().Contain(x => x.WorkflowName == "wf-a" && x.ActiveVersion == 1);
            RuleSetWorkflowSummary wfB = listPayload.Single(x => x.WorkflowName == "wf-b");
            wfB.ActiveVersion.Should().Be(2);
            wfB.Versions.Should().Equal(1, 2);

            IActionResult versionsResult = await controller.GetVersions("wf-b");
            OkObjectResult versionsOk = versionsResult.Should().BeOfType<OkObjectResult>().Subject;
            RuleSetWorkflowSummary versionsPayload = versionsOk.Value.Should().BeOfType<RuleSetWorkflowSummary>().Subject;
            versionsPayload.WorkflowName.Should().Be("wf-b");
            versionsPayload.ActiveVersion.Should().Be(2);
            versionsPayload.Versions.Should().Equal(1, 2);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Save_ShouldReturnBadRequest_WhenValidationFails()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            SystemExecutionContextAccessor accessor = new();
            RuntimeRuleSetController controller = CreateController(
                new RulesEngineService(new FileRuleSetStore(root, executionContextAccessor: accessor), executionContextAccessor: accessor),
                new FileRuleSetAuditStore(root, new MJsonSerializeService(), accessor),
                accessor);

            IActionResult result = await controller.Save("wf-expected", new SaveRuleSetRequest
            {
                RuleSet = ParseJson("""{"workflowName":"wf-other","rules":["RULE_A"]}""")
            });

            BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            RuleSetValidationResult payload = badRequest.Value.Should().BeOfType<RuleSetValidationResult>().Subject;
            payload.Errors.Should().Contain(x => x.Code == "WorkflowMismatch");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Activate_And_Audit_ShouldReturnUpdatedVersionAndStoredEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            SystemExecutionContextAccessor accessor = new();
            FileRuleSetStore store = new(root, executionContextAccessor: accessor);
            RulesEngineService service = new(store, executionContextAccessor: accessor);
            FileRuleSetAuditStore auditStore = new(root, new MJsonSerializeService(), accessor);
            RuntimeRuleSetController controller = CreateController(service, auditStore, accessor);

            SetTenant(accessor, "tenant-activate");
            await service.SaveRuleSetAsync("wf-activate", """{"workflowName":"wf-activate","rules":["RULE_A"]}""");
            await service.SaveRuleSetAsync("wf-activate", """{"workflowName":"wf-activate","rules":["RULE_B"]}""");
            await service.SetActiveVersionAsync("wf-activate", 1);

            IActionResult activateResult = await controller.Activate(
                "wf-activate",
                2,
                new ActivateRuleSetRequest
                {
                    Actor = "approver",
                    Detail = "manual switch"
                });

            OkObjectResult activateOk = activateResult.Should().BeOfType<OkObjectResult>().Subject;
            RuleSetWorkflowSummary activatePayload = activateOk.Value.Should().BeOfType<RuleSetWorkflowSummary>().Subject;
            activatePayload.ActiveVersion.Should().Be(2);
            activatePayload.Versions.Should().Equal(1, 2);

            IActionResult auditResult = await controller.Audit("wf-activate");
            OkObjectResult auditOk = auditResult.Should().BeOfType<OkObjectResult>().Subject;
            RuleSetAuditPage auditPayload = auditOk.Value.Should().BeOfType<RuleSetAuditPage>().Subject;
            auditPayload.Items.Should().Contain(x =>
                x.Action == "Activate" &&
                x.Actor == "approver" &&
                x.Detail == "manual switch" &&
                x.TenantId == "tenant-activate");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task DryRun_ShouldReturnBadRequest_WhenValidationFails()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            SystemExecutionContextAccessor accessor = new();
            RuntimeRuleSetController controller = CreateController(
                new RulesEngineService(new FileRuleSetStore(root, executionContextAccessor: accessor), executionContextAccessor: accessor),
                new FileRuleSetAuditStore(root, new MJsonSerializeService(), accessor),
                accessor);

            IActionResult result = await controller.DryRun("wf-expected", new DryRunRuleSetRequest
            {
                RuleSet = ParseJson("""{"workflowName":"wf-other","rules":["RULE_A"]}"""),
                Context = ParseJson("""{"amount":10}""")
            });

            BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            RuleSetValidationResult payload = badRequest.Value.Should().BeOfType<RuleSetValidationResult>().Subject;
            payload.Errors.Should().Contain(x => x.Code == "WorkflowMismatch");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static RuntimeRuleSetController CreateController(
        RulesEngineService service,
        IRuleSetAuditStore auditStore,
        ISystemExecutionContextAccessor accessor)
    {
        return new RuntimeRuleSetController(service, auditStore, accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static JsonElement ParseJson(string json)
    {
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static void SetTenant(ISystemExecutionContextAccessor accessor, string tenantId)
    {
        accessor.Set(new SystemExecutionContext(
            tenantId,
            userId: null,
            username: null,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "tests"));
    }
}
