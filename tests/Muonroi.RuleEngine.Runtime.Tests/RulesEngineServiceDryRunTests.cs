using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.RuleEngine.Runtime.Adapters;
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

    [Fact]
    public async Task DryRunAsync_CodeWorkflowWithResolvedContextType_ShouldExecuteRule()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);

        const string json = """
                            {
                              "workflowName": "CodeWorkflow",
                              "rules": [ "DOUBLE_INPUT" ]
                            }
                            """;

        JsonElement context = JsonDocument.Parse("""{"Value":4}""").RootElement.Clone();
        FactBag result = await service.DryRunAsync(
            "CodeWorkflow",
            json,
            context,
            typeof(DryRunCodeContext).FullName);

        result.Get<int>("DoubleInput").Should().Be(8);
    }

    [Fact]
    public async Task DryRunAsync_CodeWorkflowWithUnknownContextType_ShouldThrow()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);

        const string json = """
                            {
                              "workflowName": "CodeWorkflow",
                              "rules": [ "DOUBLE_INPUT" ]
                            }
                            """;

        JsonElement context = JsonDocument.Parse("""{"Value":4}""").RootElement.Clone();
        Func<Task> action = async () => await service.DryRunAsync(
            "CodeWorkflow",
            json,
            context,
            "Muonroi.RuleEngine.Runtime.Tests.DoesNotExistContext");

        await action.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Cannot resolve contextType*");
    }

    [Fact]
    public async Task DryRunAsync_CodeWorkflowWithUnknownRuleCode_ShouldThrow()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);

        const string json = """
                            {
                              "workflowName": "CodeWorkflow",
                              "rules": [ "DOES_NOT_EXIST" ]
                            }
                            """;

        JsonElement context = JsonDocument.Parse("""{"Value":4}""").RootElement.Clone();
        Func<Task> action = async () => await service.DryRunAsync(
            "CodeWorkflow",
            json,
            context,
            typeof(DryRunCodeContext).FullName);

        await action.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*no rule implementations were discovered*");
    }

    [Fact]
    public async Task DryRunAsync_LegacyWorkflowWithNullContext_ShouldSupportNullInput()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);

        const string json = """
                            [
                              {
                                "WorkflowName": "NullContextWorkflow",
                                "Rules": [
                                  {
                                    "RuleName": "WhenInputIsNull",
                                    "RuleExpressionType": "LambdaExpression",
                                    "Expression": "true",
                                    "Actions": {
                                      "OnSuccess": {
                                        "Name": "OutputExpression",
                                        "Context": {
                                          "expression": "1"
                                        }
                                      }
                                    }
                                  }
                                ]
                              }
                            ]
                            """;

        JsonElement context = JsonDocument.Parse("null").RootElement.Clone();
        FactBag result = await service.DryRunAsync("NullContextWorkflow", json, context);

        result.Get<int>("WhenInputIsNull").Should().Be(1);
    }

    [Fact]
    public async Task DryRunAsync_GraphWorkflowWithUnknownContextType_ShouldThrow()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(dir);
        RuleGraphParser parser = new(new MJsonSerializeService());
        RulesEngineService service = new(store, graphParser: parser);

        const string json = """
                            {
                              "workflowName": "GraphWorkflow",
                              "flowGraph": {
                                "nodes": [
                                  { "id": "trigger", "type": "trigger", "data": {} },
                                  { "id": "end", "type": "end", "data": {} }
                                ],
                                "edges": [
                                  { "id": "e1", "source": "trigger", "target": "end" }
                                ]
                              }
                            }
                            """;

        JsonElement context = JsonDocument.Parse("""{"value":1}""").RootElement.Clone();
        Func<Task> action = async () => await service.DryRunAsync(
            "GraphWorkflow",
            json,
            context,
            "Muonroi.RuleEngine.Runtime.Tests.DoesNotExistGraphContext");

        await action.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Cannot resolve contextType*");
    }

    public sealed class DryRunCodeContext
    {
        public int Value { get; init; }
    }

    private sealed class DoubleInputRule : IRule<DryRunCodeContext>
    {
        public string Code => "DOUBLE_INPUT";
        public int Order => 0;
        public string[] DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Business;
        public string Name => nameof(DoubleInputRule);
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(DryRunCodeContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("DoubleInput", ctx.Value * 2);
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(DryRunCodeContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
