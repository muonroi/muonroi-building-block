using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Web.Controllers;
using Muonroi.RuleEngine.Runtime.Web.Services;
using System.Text.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class MRuleFlowExecuteControllerTests
{
    // Concrete subclass for testing the abstract base controller
    private sealed class TestableRuleFlowExecuteController(
        RulesEngineService rulesEngineService,
        IRuleDryRunService dryRunService,
        ISystemExecutionContextAccessor executionContextAccessor)
        : MRuleFlowExecuteController(rulesEngineService, dryRunService, executionContextAccessor);

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRulesetDoesNotExist()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            SystemExecutionContextAccessor accessor = new();
            RulesEngineService rulesEngineService = new(new FileRuleSetStore(root, executionContextAccessor: accessor), executionContextAccessor: accessor);
            RecordingRuleDryRunService dryRunService = new();
            TestableRuleFlowExecuteController controller = new(rulesEngineService, dryRunService, accessor);

            IActionResult result = await controller.ExecuteAsync(
                "wf-missing",
                null,
                JsonDocument.Parse("""{"amount":10}""").RootElement.Clone(),
                default);

            NotFoundObjectResult notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            JsonElement payload = JsonSerializer.SerializeToElement(notFound.Value);
            payload.GetProperty("message").GetString().Should().Contain("wf-missing");
            dryRunService.CallCount.Should().Be(0);
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
    public async Task ExecuteAsync_ShouldNormalizeFacts_AndMapDryRunResponse()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            SystemExecutionContextAccessor accessor = new();
            FileRuleSetStore store = new(root, executionContextAccessor: accessor);
            RulesEngineService rulesEngineService = new(store, executionContextAccessor: accessor);
            accessor.Set(new SystemExecutionContext(
                tenantId: "tenant-42",
                userId: null,
                username: null,
                correlationId: Guid.NewGuid().ToString("N"),
                accessToken: null,
                apiKey: null,
                isAuthenticated: false,
                permissions: [],
                sourceType: "tests"));
            await store.SaveAsync("wf-exec", """{"workflowName":"wf-exec","rules":["RULE_A"]}""");
            await store.SetActiveVersionAsync("wf-exec", 1);

            RecordingRuleDryRunService dryRunService = new()
            {
                Result = new RuleDryRunResult
                {
                    RulesMatched = true,
                    Errors = [],
                    OutputFacts = new Dictionary<string, object?> { ["result"] = "passed" },
                    Traces =
                    [
                        new RuleExecutionTrace
                        {
                            RuleName = "RULE_A",
                            Matched = true
                        }
                    ]
                }
            };

            TestableRuleFlowExecuteController controller = new(rulesEngineService, dryRunService, accessor);
            JsonElement inputFacts = JsonDocument.Parse(
                """{"amount":10,"enabled":true,"items":[1,2],"profile":{"name":"Muonroi"}}""").RootElement.Clone();

            IActionResult result = await controller.ExecuteAsync("wf-exec", null, inputFacts, default);

            OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
            JsonElement payload = JsonSerializer.SerializeToElement(ok.Value);
            payload.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
            payload.GetProperty("factBag").GetProperty("result").GetString().Should().Be("passed");
            payload.GetProperty("results")[0].GetProperty("ruleName").GetString().Should().Be("RULE_A");

            dryRunService.CallCount.Should().Be(1);
            dryRunService.LastTenantId.Should().Be("tenant-42");
            dryRunService.LastFormat.Should().Be(RuleSetFormat.Json);
            dryRunService.LastRuleSetContent.Should().Contain("\"workflowName\":\"wf-exec\"");
            dryRunService.LastFacts["amount"].Should().Be(10L);
            dryRunService.LastFacts["enabled"].Should().Be(true);
            ((List<object?>)dryRunService.LastFacts["items"]!).Should().ContainInOrder(1L, 2L);
            ((Dictionary<string, object?>)dryRunService.LastFacts["profile"]!)["name"].Should().Be("Muonroi");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class RecordingRuleDryRunService : IRuleDryRunService
    {
        public int CallCount { get; private set; }

        public string LastRuleSetContent { get; private set; } = string.Empty;

        public RuleSetFormat LastFormat { get; private set; }

        public Dictionary<string, object?> LastFacts { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public string? LastTenantId { get; private set; }

        public RuleDryRunResult Result { get; init; } = new();

        public Task<RuleDryRunResult> RunAsync(
            string ruleSetContent,
            RuleSetFormat format,
            IReadOnlyDictionary<string, object?> inputFacts,
            string? tenantId = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRuleSetContent = ruleSetContent;
            LastFormat = format;
            LastFacts = new Dictionary<string, object?>(inputFacts, StringComparer.OrdinalIgnoreCase);
            LastTenantId = tenantId;
            return Task.FromResult(Result);
        }
    }
}
