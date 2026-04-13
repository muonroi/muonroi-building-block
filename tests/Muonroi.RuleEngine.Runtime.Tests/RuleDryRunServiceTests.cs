using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Web.Services;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleDryRunServiceTests
{
    [Fact]
    public async Task RunAsync_ShouldReturnMatchedTrace_ForLegacyWorkflow()
    {
        RuleDryRunService service = CreateService();
        const string ruleSet = """
                               [
                                 {
                                   "WorkflowName": "wf.legacy",
                                   "Rules": [
                                     {
                                       "RuleName": "AlwaysMatch",
                                       "RuleExpressionType": "LambdaExpression",
                                       "Expression": "true",
                                       "Actions": {
                                         "OnSuccess": {
                                           "Name": "OutputExpression",
                                           "Context": {
                                             "expression": "5"
                                           }
                                         }
                                       }
                                     }
                                   ]
                                 }
                               ]
                               """;

        RuleDryRunResult result = await service.RunAsync(
            ruleSet,
            RuleSetFormat.Json,
            new Dictionary<string, object?>());

        result.RulesMatched.Should().BeTrue();
        result.Traces.Should().ContainSingle(x => x.RuleName == "AlwaysMatch" && x.Matched);
        result.OutputFacts.Should().ContainKey("AlwaysMatch");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_ShouldReturnNoMatch_ForLegacyWorkflow()
    {
        RuleDryRunService service = CreateService();
        const string ruleSet = """
                               [
                                 {
                                   "WorkflowName": "wf.legacy",
                                   "Rules": [
                                     {
                                       "RuleName": "NeverMatch",
                                       "RuleExpressionType": "LambdaExpression",
                                       "Expression": "false"
                                     }
                                   ]
                                 }
                               ]
                               """;

        RuleDryRunResult result = await service.RunAsync(
            ruleSet,
            RuleSetFormat.Json,
            new Dictionary<string, object?>());

        result.RulesMatched.Should().BeFalse();
        result.Traces.Should().ContainSingle(x => x.RuleName == "NeverMatch" && !x.Matched);
    }

    [Fact]
    public async Task RunAsync_ShouldReturnValidationErrors_ForInvalidRuleset()
    {
        RuleDryRunService service = CreateService();

        RuleDryRunResult result = await service.RunAsync(
            """{ "workflowName": "wf.invalid" """,
            RuleSetFormat.Json,
            new Dictionary<string, object?>());

        result.RulesMatched.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunAsync_ShouldReturnUnsupportedFormatError_WhenNotJson()
    {
        RuleDryRunService service = CreateService();

        RuleDryRunResult result = await service.RunAsync(
            """{ "workflowName": "wf.xml", "rules": ["RULE_A"] }""",
            RuleSetFormat.Xml,
            new Dictionary<string, object?>());

        result.RulesMatched.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("not supported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_ShouldReturnTraceAndError_ForUnknownCodeRule()
    {
        RuleDryRunService service = CreateService();

        RuleDryRunResult result = await service.RunAsync(
            """{ "workflowName": "wf.code", "rules": ["RULE_A"] }""",
            RuleSetFormat.Json,
            new Dictionary<string, object?>());

        result.RulesMatched.Should().BeFalse();
        result.Traces.Should().ContainSingle(x => x.RuleName == "RULE_A" && !x.Matched);
        result.Errors.Should().NotBeEmpty();
    }

    private static RuleDryRunService CreateService()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        SystemExecutionContextAccessor accessor = new();
        FileRuleSetStore store = new(root, executionContextAccessor: accessor);
        RuleSetDefinitionValidator validator = new();
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        return new RuleDryRunService(store, validator, provider, accessor);
    }
}
