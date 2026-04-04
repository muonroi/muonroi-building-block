using Muonroi.Core.Abstractions.Exceptions;
using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleGen.Models;
using Muonroi.RuleGen.Services;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class RuntimeRuleJsonServiceTests
{
    [Fact]
    public void Load_WithObjectMetadata_Parses_Workflow_Version_And_Rules()
    {
        string path = CreateTempJsonFile(
            """
            {
              "workflowName": "wf.orders",
              "version": "3",
              "tenantId": "tenant-a",
              "rules": [
                {
                  "code": "RULE_001",
                  "name": "Validate order",
                  "order": 5,
                  "hookPoint": "AfterRule",
                  "dependsOn": ["RULE_000"],
                  "condition": "ctx.amount > 0",
                  "action": "facts.result = true",
                  "type": "Business",
                  "source": "file-a"
                }
              ]
            }
            """);

        try
        {
            var result = RuntimeRuleJsonService.Load(path, defaultWorkflow: null, tenantId: null);

            result.WorkflowName.Should().Be("wf.orders");
            result.Version.Should().Be(3);
            result.TenantId.Should().Be("tenant-a");
            result.Rules.Should().ContainSingle();
            result.Rules[0].Code.Should().Be("RULE_001");
            result.Rules[0].DependsOn.Should().ContainSingle().Which.Should().Be("RULE_000");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WithStringArray_Creates_Legacy_Rules()
    {
        string path = CreateTempJsonFile("""["RULE_A","RULE_B"]""");

        try
        {
            var result = RuntimeRuleJsonService.Load(path, defaultWorkflow: "wf.legacy", tenantId: "tenant-b");

            result.WorkflowName.Should().Be("wf.legacy");
            result.Rules.Should().HaveCount(2);
            result.Rules[0].Code.Should().Be("RULE_A");
            result.Rules[0].Source.Should().Be("legacy-string");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WithoutAnyRules_Throws()
    {
        string path = CreateTempJsonFile("""{ "workflowName": "wf.empty", "rules": [] }""");

        try
        {
            Action act = () => RuntimeRuleJsonService.Load(path, defaultWorkflow: null, tenantId: null);
            act.Should().Throw<MConfigurationException>()
                .WithMessage("*does not contain any rules*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_Writes_Serializable_Runtime_Rule_Set()
    {
        string json = RuntimeRuleJsonService.Export(
            "wf.export",
            2,
            "tenant-x",
            [
                new("RULE_001", "Rule 1", 1, "BeforeRule", ["RULE_000"], "ctx.ready", "facts.done = true", "Validation", "source-a")
            ]);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("workflowName").GetString().Should().Be("wf.export");
        root.GetProperty("version").GetInt32().Should().Be(2);
        root.GetProperty("tenantId").GetString().Should().Be("tenant-x");
        root.GetProperty("rules")[0].GetProperty("code").GetString().Should().Be("RULE_001");
    }

    private static string CreateTempJsonFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"rulegen-runtime-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        return path;
    }
}
