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

        // Updated: pass empty outputFacts dict (4th param)
        IReadOnlyDictionary<string, object?> emptyFacts = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        List<RuleExecutionTrace> traces = (List<RuleExecutionTrace>)InvokePrivateStatic(
            "BuildCodeTraces",
            new[] { "RULE_A", "RULE_B" },
            entries,
            Array.Empty<string>(),
            emptyFacts)!;
        List<RuleExecutionTrace> fallback = (List<RuleExecutionTrace>)InvokePrivateStatic(
            "BuildCodeTraces",
            new[] { "RULE_X" },
            Array.Empty<RuleTraceEntry>(),
            new[] { "shared failure" },
            emptyFacts)!;

        traces.Should().Contain(x => x.RuleName == "RULE_A" && x.Matched);
        traces.Should().Contain(x => x.RuleName == "RULE_B" && !x.Matched && x.FailReason == "boom");
        fallback.Should().ContainSingle(x => x.RuleName == "RULE_X" && x.FailReason == "shared failure");
    }

    [Fact]
    public void BuildCodeTraces_EdgeScopedOverride_ShouldReplaceInputOutputJsonWhenFactBagHasTraceKeys()
    {
        // Arrange: outputFacts contains __trace.node.RULE_A.input and .output (edge-scoped data)
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<RuleTraceEntry> entries =
        [
            new()
            {
                RuleName = "RULE_A",
                Phase = RuleTracePhase.AfterEval,
                ExecutedAt = now,
                IsSuccess = true,
                InputFactsJson = """{"old":"input"}""",
                OutputFactsJson = """{"old":"output"}"""
            }
        ];

        Dictionary<string, object?> edgeScopedInput = new(StringComparer.OrdinalIgnoreCase) { ["foo"] = "bar" };
        Dictionary<string, object?> edgeScopedOutput = new(StringComparer.OrdinalIgnoreCase) { ["result"] = "ok" };

        Dictionary<string, object?> outputFacts = new(StringComparer.OrdinalIgnoreCase)
        {
            [$"__trace.node.RULE_A.input"] = edgeScopedInput,
            [$"__trace.node.RULE_A.output"] = edgeScopedOutput
        };

        // Act
        List<RuleExecutionTrace> traces = (List<RuleExecutionTrace>)InvokePrivateStatic(
            "BuildCodeTraces",
            new[] { "RULE_A" },
            entries,
            Array.Empty<string>(),
            (IReadOnlyDictionary<string, object?>)outputFacts)!;

        // Assert: InputFactsJson and OutputFactsJson should be overridden with edge-scoped data
        RuleExecutionTrace trace = traces.Should().ContainSingle(x => x.RuleName == "RULE_A").Subject;
        trace.InputFactsJson.Should().NotBe("""{"old":"input"}""",
            "BuildCodeTraces should override with edge-scoped __trace.node.RULE_A.input");
        trace.InputFactsJson.Should().Contain("foo",
            "edge-scoped input key 'foo' should appear in serialized InputFactsJson");
        trace.OutputFactsJson.Should().Contain("result",
            "edge-scoped output key 'result' should appear in serialized OutputFactsJson");
    }

    [Fact]
    public void BuildCodeTraces_EdgeScopedOverride_ShouldFallBackToTracerValueWhenFactBagKeyMissing()
    {
        // Arrange: outputFacts does NOT contain __trace.node.RULE_B.input — fallback to tracer entry value
        DateTimeOffset now = DateTimeOffset.UtcNow;
        const string originalInput = """{"original":"value"}""";
        List<RuleTraceEntry> entries =
        [
            new()
            {
                RuleName = "RULE_B",
                Phase = RuleTracePhase.AfterEval,
                ExecutedAt = now,
                IsSuccess = true,
                InputFactsJson = originalInput
            }
        ];

        // outputFacts has NO __trace.node.RULE_B.input key
        IReadOnlyDictionary<string, object?> outputFacts = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Act
        List<RuleExecutionTrace> traces = (List<RuleExecutionTrace>)InvokePrivateStatic(
            "BuildCodeTraces",
            new[] { "RULE_B" },
            entries,
            Array.Empty<string>(),
            outputFacts)!;

        // Assert: InputFactsJson retains original tracer value when no edge-scoped override present
        RuleExecutionTrace trace = traces.Should().ContainSingle(x => x.RuleName == "RULE_B").Subject;
        trace.InputFactsJson.Should().Be(originalInput,
            "InputFactsJson should fall back to tracer entry value when no __trace.node.* key in outputFacts");
    }

    [Fact]
    public void BuildCodeTraces_PathBParity_ShouldProduceSameInputJsonShapeAsFactBagRead()
    {
        // D-08/DRTRACE-05: Both Path A (BuildCodeTraces) and Path B (MapOrchestratorToMDryRunResponse)
        // read from __trace.node.{id}.input in outputFacts/FactBag.
        // This test verifies that BuildCodeTraces produces the same JSON shape that Path B would see
        // when reading the same key directly from FactBag.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string nodeId = "RULE_PARITY";

        List<RuleTraceEntry> entries =
        [
            new()
            {
                RuleName = nodeId,
                Phase = RuleTracePhase.AfterEval,
                ExecutedAt = now,
                IsSuccess = true
            }
        ];

        // The FactBag key value that GraphRuleDispatchAdapter writes
        Dictionary<string, object?> edgeScopedData = new(StringComparer.OrdinalIgnoreCase)
        {
            ["foo"] = "bar",
            ["count"] = 42
        };

        Dictionary<string, object?> outputFacts = new(StringComparer.OrdinalIgnoreCase)
        {
            [$"__trace.node.{nodeId}.input"] = edgeScopedData
        };

        // Path A: BuildCodeTraces serializes the dict
        List<RuleExecutionTrace> traces = (List<RuleExecutionTrace>)InvokePrivateStatic(
            "BuildCodeTraces",
            new[] { nodeId },
            entries,
            Array.Empty<string>(),
            (IReadOnlyDictionary<string, object?>)outputFacts)!;

        // Path B: MapOrchestratorToMDryRunResponse would read the same key and serialize it
        string pathBJson = JsonSerializer.Serialize(edgeScopedData);

        // Both paths should produce the same JSON shape
        RuleExecutionTrace trace = traces.Should().ContainSingle(x => x.RuleName == nodeId).Subject;
        trace.InputFactsJson.Should().Be(pathBJson,
            "Path A (BuildCodeTraces) and Path B (MapOrchestratorToMDryRunResponse) should produce identical InputFactsJson from the same __trace.node.{id}.input data");
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
