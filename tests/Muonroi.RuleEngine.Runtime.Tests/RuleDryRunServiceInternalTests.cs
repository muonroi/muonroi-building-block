using FluentAssertions;
using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.Core.Tracing;
using Muonroi.RuleEngine.Runtime.Web.Services;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleDryRunServiceInternalTests
{
    [Fact]
    public void ExtractContextType_RemovesReservedKey_AndTrimsValue()
    {
        Dictionary<string, object?> inputs = new(StringComparer.OrdinalIgnoreCase)
        {
            ["__contextType"] = "  My.Namespace.Context  ",
            ["amount"] = 10
        };

        string? result = (string?)InvokePrivateStatic("ExtractContextType", inputs);

        result.Should().Be("My.Namespace.Context");
        inputs.Should().NotContainKey("__contextType");
        inputs.Should().ContainKey("amount");
    }

    [Fact]
    public void DeserializeLegacyWorkflows_CanWrapSingleRuleObject()
    {
        const string json = """
                            {
                              "workflowName": "wf-single",
                              "ruleName": "Always",
                              "expression": "true"
                            }
                            """;

        object[] workflows = (object[])(InvokePrivateStatic("DeserializeLegacyWorkflows", json) ?? Array.Empty<object>());

        workflows.Should().HaveCount(1);
    }

    [Fact]
    public void ParseMetadata_RecognizesFlowGraphAndCodeRulesets()
    {
        object graph = InvokePrivateStatic("ParseMetadata", """{"workflowName":"wf-graph","flowGraph":{"nodes":[],"edges":[]}}""")!;
        object code = InvokePrivateStatic("ParseMetadata", """{"workflowName":"wf-code","rules":["A","A","B"]}""")!;

        GetProperty<bool>(graph, "IsLegacyWorkflow").Should().BeFalse();
        GetProperty<string>(graph, "WorkflowName").Should().Be("wf-graph");
        GetProperty<IReadOnlyList<string>>(code, "RuleCodes").Should().Equal("A", "B");
    }

    [Fact]
    public void BuildInvalidValidationTraces_ReturnsEntries_ForDeclaredCodeRules()
    {
        object metadata = InvokePrivateStatic("ParseMetadata", """{"workflowName":"wf-code","rules":["RULE_A","RULE_B"]}""")!;

        List<RuleExecutionTrace> traces = (List<RuleExecutionTrace>)InvokePrivateStatic("BuildInvalidValidationTraces", metadata)!;

        traces.Should().HaveCount(2);
        traces.Should().Contain(x => x.RuleName == "RULE_A" && x.FailReason == "Ruleset validation failed.");
        traces.Should().Contain(x => x.RuleName == "RULE_B" && x.FailReason == "Ruleset validation failed.");
    }

    [Fact]
    public void BuildCodeTraces_UsesLatestAfterEvalResult_AndFallsBackToSharedError()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<RuleTraceEntry> entries =
        [
            new()
            {
                RuleName = "RULE_A",
                Phase = RuleTracePhase.BeforeEval,
                ExecutedAt = now.AddSeconds(-2),
                IsSuccess = false
            },
            new()
            {
                RuleName = "RULE_A",
                Phase = RuleTracePhase.AfterEval,
                ExecutedAt = now.AddSeconds(-1),
                IsSuccess = true
            },
            new()
            {
                RuleName = "RULE_B",
                Phase = RuleTracePhase.Error,
                ExecutedAt = now,
                IsSuccess = false,
                FailureReason = "boom"
            }
        ];

        List<RuleExecutionTrace> traces = (List<RuleExecutionTrace>)InvokePrivateStatic(
            "BuildCodeTraces",
            new[] { "RULE_A", "RULE_B" },
            entries,
            Array.Empty<string>())!;
        List<RuleExecutionTrace> fallback = (List<RuleExecutionTrace>)InvokePrivateStatic(
            "BuildCodeTraces",
            new[] { "RULE_X" },
            Array.Empty<RuleTraceEntry>(),
            new[] { "shared failure" })!;

        traces.Should().Contain(x => x.RuleName == "RULE_A" && x.Matched);
        traces.Should().Contain(x => x.RuleName == "RULE_B" && !x.Matched && x.FailReason == "boom");
        fallback.Should().ContainSingle(x => x.RuleName == "RULE_X" && x.FailReason == "shared failure");
    }

    [Fact]
    public void NormalizeInputs_And_NormalizeOutputValue_ConvertNestedJson()
    {
        IReadOnlyDictionary<string, object?> inputs = new Dictionary<string, object?>
        {
            ["payload"] = JsonDocument.Parse("""{"flag":true,"items":[1,2.5]}""").RootElement.Clone()
        };
        JsonElement output = JsonDocument.Parse("""{"code":"A1","nested":{"count":3}}""").RootElement.Clone();

        Dictionary<string, object?> normalizedInputs =
            (Dictionary<string, object?>)InvokePrivateStatic("NormalizeInputs", inputs)!;
        object? normalizedOutput = InvokePrivateStatic("NormalizeOutputValue", output);

        Dictionary<string, object?> payload =
            normalizedInputs["payload"].Should().BeOfType<Dictionary<string, object?>>().Subject;
        payload["flag"].Should().Be(true);
        ((List<object?>)payload["items"]!).Should().ContainInOrder(1, 2.5m);
        ((Dictionary<string, object?>)normalizedOutput!)["code"].Should().Be("A1");
    }

    [Fact]
    public void WithTenant_CanWrapNonSystemExecutionContext()
    {
        TestExecutionContext current = new();

        ISystemExecutionContext result =
            (ISystemExecutionContext)InvokePrivateStatic("WithTenant", current, "tenant-b")!;

        result.TenantId.Should().Be("tenant-b");
        result.Username.Should().Be("tester");
        result.CorrelationId.Should().Be("corr-1");
    }

    private static object? InvokePrivateStatic(string methodName, params object[] args)
    {
        MethodInfo method = typeof(RuleDryRunService).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.Invoke(null, args);
    }

    private static T GetProperty<T>(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return (T)property.GetValue(instance)!;
    }

    private sealed class TestExecutionContext : ISystemExecutionContext
    {
        public string? TenantId => null;
        public string? UserId => null;
        public string? Username => "tester";
        public string CorrelationId => "corr-1";
        public string? AccessToken => null;
        public string? ApiKey => null;
        public bool IsAuthenticated => false;
        public IReadOnlyList<string> Permissions => Array.Empty<string>();
        public string SourceType => "tests";
    }
}
