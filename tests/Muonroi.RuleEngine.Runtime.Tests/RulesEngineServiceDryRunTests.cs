using FluentAssertions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Runtime.Rules;
using System.Text.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RulesEngineServiceDryRunTests
{
    [Fact]
    public async Task DryRunAsync_ShouldEvaluateLegacyWorkflowAndReturnFacts()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);

        const string json = """
                            [
                              {
                                "WorkflowName": "DryRunWorkflow",
                                "Rules": [
                                  {
                                    "RuleName": "Double",
                                    "RuleExpressionType": "LambdaExpression",
                                    "Expression": "input1.value > 0",
                                    "Actions": {
                                      "OnSuccess": {
                                        "Name": "OutputExpression",
                                        "Context": {
                                          "expression": "input1.value * 2"
                                        }
                                      }
                                    }
                                  }
                                ]
                              }
                            ]
                            """;

        JsonElement context = JsonDocument.Parse("3").RootElement.Clone();
        FactBag result = await service.DryRunAsync("DryRunWorkflow", json, context);

        result.Get<int>("Double").Should().Be(6);
    }

    [Fact]
    public async Task DryRunAsync_CodeWorkflowWithoutContextType_ShouldThrow()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);

        const string json = """
                            {
                              "workflowName": "CodeWorkflow",
                              "rules": [ "RULE_A" ]
                            }
                            """;

        JsonElement context = JsonDocument.Parse("{\"value\":1}").RootElement.Clone();
        Func<Task> action = async () => await service.DryRunAsync("CodeWorkflow", json, context);

        await action.Should().ThrowAsync<InvalidDataException>();
    }
}
