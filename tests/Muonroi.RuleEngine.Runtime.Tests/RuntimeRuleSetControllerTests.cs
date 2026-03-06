using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Web.Controllers;
using Muonroi.RuleEngine.Runtime.Web.ViewModels;
using Muonroi.Tenancy.Core;
using System.Text.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuntimeRuleSetControllerTests
{
    [Fact]
    public async Task Save_Then_Export_ShouldReturnPersistedRuleset()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(root);
        RulesEngineService service = new(store);
        FileRuleSetAuditStore auditStore = new(root, new MJsonSerializeService());

        RuntimeRuleSetController controller = new(service, auditStore)
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

        TenantContext.CurrentTenantId = "tenant-api";
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
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task Validate_ShouldReturnErrors_WhenWorkflowMismatched()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        RulesEngineService service = new(new FileRuleSetStore(root));
        RuntimeRuleSetController controller = new(service, new FileRuleSetAuditStore(root, new MJsonSerializeService()))
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
}
