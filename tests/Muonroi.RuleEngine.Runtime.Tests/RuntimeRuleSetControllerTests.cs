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
        SystemExecutionContextAccessor accessor = new();
        FileRuleSetStore store = new(root, executionContextAccessor: accessor);
        RulesEngineService service = new(store, executionContextAccessor: accessor);
        FileRuleSetAuditStore auditStore = new(root, new MJsonSerializeService(), accessor);

        RuntimeRuleSetController controller = new(service, auditStore, accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.ControllerContext.HttpContext.Request.Headers["X-Actor"] = "runtime-test";

        const string ruleSetText = """
                                   {
                                     "workflowName": "wf-api",
                                     "rules": [ "RULE_A" ]
                                   }
                                   """;
        SaveRuleSetRequest saveRequest = new()
        {
            RuleSet = JsonDocument.Parse(ruleSetText).RootElement.Clone(),
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
