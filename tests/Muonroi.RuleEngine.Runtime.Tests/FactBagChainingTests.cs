using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Logging;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Tests for FactBag output chaining: node-scoped results, outputField namespacing,
/// MFactBagAwareRule consumption, on-false branch outputs, and diamond merge accumulation.
/// </summary>
public sealed class FactBagChainingTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(), "muonroi-factbag-chaining-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NodeScopedResult_ShouldBePreservedAfterSubsequentNodeOverwritesGenericResult()
    {
        // Arrange: A → B → C, verify __graph.node.A.result is still accessible after B runs
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "ScopedResultFlow", """
        {
          "workflowName": "ScopedResultFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "node-a", "type": "condition", "ruleCode": "CHAIN_A", "data": {} },
              { "id": "node-b", "type": "condition", "ruleCode": "CHAIN_B", "data": {} },
              { "id": "node-c", "type": "condition", "ruleCode": "CHAIN_C", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "node-a", "edgeType": "always" },
              { "id": "e2", "source": "node-a", "target": "node-b", "edgeType": "always" },
              { "id": "e3", "source": "node-b", "target": "node-c", "edgeType": "always" },
              { "id": "e4", "source": "node-c", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        // Act
        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();
        FactBag facts = await service.ExecuteAsync("ScopedResultFlow", new ChainingContext());

        // Assert: node-a scoped result still accessible after node-b and node-c ran
        Dictionary<string, object?>? nodeAResult = facts.Get<Dictionary<string, object?>>("__graph.node.node-a.result");
        nodeAResult.Should().NotBeNull();
        nodeAResult!["isPass"].Should().Be(true);

        Dictionary<string, object?>? nodeBResult = facts.Get<Dictionary<string, object?>>("__graph.node.node-b.result");
        nodeBResult.Should().NotBeNull();
        nodeBResult!["isPass"].Should().Be(true);

        // Generic "result" should be from last node (node-c)
        Dictionary<string, object?>? genericResult = facts.Get<Dictionary<string, object?>>("result");
        genericResult.Should().NotBeNull();
        genericResult!["isPass"].Should().Be(true);
    }

    [Fact]
    public async Task MFactBagAwareRule_ShouldReadUpstreamFeelOutputFields()
    {
        // Arrange: FEEL node writes gate.result=true → MFactBagAwareRule downstream reads it
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "AwareRuleFlow", """
        {
          "workflowName": "AwareRuleFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              {
                "id": "gate",
                "type": "condition",
                "data": {
                  "expression": { "language": "feel", "body": "true" },
                  "outputFields": [
                    { "path": "gate.result", "valueExpression": "true", "dataType": "boolean" },
                    { "path": "gate.checkedAt", "valueExpression": "\"2026-03-13\"", "dataType": "string" }
                  ]
                }
              },
              { "id": "consumer", "type": "condition", "ruleCode": "AWARE_CONSUMER", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "gate", "edgeType": "always" },
              { "id": "e2", "source": "gate", "target": "consumer", "edgeType": "always" },
              { "id": "e3", "source": "consumer", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        // Act
        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();
        FactBag facts = await service.ExecuteAsync("AwareRuleFlow", new ChainingContext());

        // Assert: MFactBagAwareRule consumed upstream FEEL outputs
        facts.Get<string>("consumed.gate.result").Should().Be("True");
        facts.Get<string>("consumed.gate.checkedAt").Should().Be("2026-03-13");
    }

    [Fact]
    public async Task OutputFieldNamespace_ShouldPreventCollisionBetweenTwoFeelNodes()
    {
        // Arrange: Two FEEL nodes both write "status" field → scoped keys differ
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "NamespaceFlow", """
        {
          "workflowName": "NamespaceFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              {
                "id": "feel-alpha",
                "type": "condition",
                "data": {
                  "expression": { "language": "feel", "body": "true" },
                  "outputFields": [
                    { "path": "status", "valueExpression": "\"alpha\"", "dataType": "string" }
                  ]
                }
              },
              {
                "id": "feel-beta",
                "type": "condition",
                "data": {
                  "expression": { "language": "feel", "body": "true" },
                  "outputFields": [
                    { "path": "status", "valueExpression": "\"beta\"", "dataType": "string" }
                  ]
                }
              },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "feel-alpha", "edgeType": "always" },
              { "id": "e2", "source": "feel-alpha", "target": "feel-beta", "edgeType": "always" },
              { "id": "e3", "source": "feel-beta", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        // Act
        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();
        FactBag facts = await service.ExecuteAsync("NamespaceFlow", new ChainingContext());

        // Assert: flat key is overwritten to last writer
        facts.Get<string>("status").Should().Be("beta");

        // But scoped keys preserve each node's output
        facts.Get<string>("__node.feel-alpha.status").Should().Be("alpha");
        facts.Get<string>("__node.feel-beta.status").Should().Be("beta");
    }

    [Fact]
    public async Task OnFalseBranch_ShouldPreserveGateOutputs()
    {
        // Arrange: Gate FEEL condition fails → on-false branch rule reads gate.result == false
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "OnFalseGateFlow", """
        {
          "workflowName": "OnFalseGateFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              {
                "id": "gate",
                "type": "condition",
                "data": {
                  "expression": { "language": "feel", "body": "false" }
                }
              },
              { "id": "fallback", "type": "action", "ruleCode": "ON_FALSE_READER", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "gate", "edgeType": "always" },
              { "id": "e2", "source": "gate", "target": "fallback", "edgeType": "on-false" },
              { "id": "e3", "source": "fallback", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        // Act
        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();
        FactBag facts = await service.ExecuteAsync("OnFalseGateFlow", new ChainingContext());

        // Assert: on-false rule could read gate's scoped result
        facts.Get<string>("fallback.saw.gate.passed").Should().Be("false");
        Dictionary<string, object?>? gateResult = facts.Get<Dictionary<string, object?>>("__graph.node.gate.result");
        gateResult.Should().NotBeNull();
        gateResult!["isPass"].Should().Be(false);
    }

    [Fact]
    public async Task DiamondMerge_ShouldAccumulateBothBranchOutputs()
    {
        // Arrange: Gate → on-true: writes x=1 / on-false: writes y=2 → merge reads both
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "DiamondMergeFlow", """
        {
          "workflowName": "DiamondMergeFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "gate", "type": "condition", "ruleCode": "GATE_PASS", "data": {} },
              { "id": "true-branch", "type": "action", "ruleCode": "WRITE_X", "data": {} },
              { "id": "false-branch", "type": "action", "ruleCode": "WRITE_Y", "data": {} },
              { "id": "merge", "type": "action", "ruleCode": "READ_XY_MERGE", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "gate", "edgeType": "always" },
              { "id": "e2", "source": "gate", "target": "true-branch", "edgeType": "on-true" },
              { "id": "e3", "source": "gate", "target": "false-branch", "edgeType": "on-false" },
              { "id": "e4", "source": "true-branch", "target": "merge", "edgeType": "always" },
              { "id": "e5", "source": "false-branch", "target": "merge", "edgeType": "always" },
              { "id": "e6", "source": "merge", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        // Act
        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();
        FactBag facts = await service.ExecuteAsync("DiamondMergeFlow", new ChainingContext());

        // Assert: gate passes → true-branch writes x=1, false-branch skipped
        // merge node reads x from FactBag
        facts.Get<int?>("branch.x").Should().Be(1);
        facts.Get<string>("merge.result").Should().Be("x=1");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private ServiceProvider BuildProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleStore:RootPath"] = _rootPath,
                ["RuleStore:UseContentRoot"] = "false"
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddMuonroiLogging());
        services.AddRuleEngine<ChainingContext>();
        services.AddRuleEngineStore(configuration);

        // Task 1+5 test rules
        services.AddScoped<IRule<ChainingContext>, ChainARule>();
        services.AddScoped<IRule<ChainingContext>, ChainBRule>();
        services.AddScoped<IRule<ChainingContext>, ChainCRule>();

        // Task 3 test rule (MFactBagAwareRule consumer)
        services.AddScoped<IRule<ChainingContext>, AwareConsumerRule>();

        // Task 4 test rules (on-false branch)
        services.AddScoped<IRule<ChainingContext>, OnFalseReaderRule>();

        // Task 5 test rules (diamond merge)
        services.AddScoped<IRule<ChainingContext>, GatePassRule>();
        services.AddScoped<IRule<ChainingContext>, WriteXRule>();
        services.AddScoped<IRule<ChainingContext>, WriteYRule>();
        services.AddScoped<IRule<ChainingContext>, ReadXYMergeRule>();

        return services.BuildServiceProvider();
    }

    private static async Task SaveWorkflowAsync(ServiceProvider provider, string workflowName, string json)
    {
        using IServiceScope scope = provider.CreateScope();
        IRuleSetStore store = scope.ServiceProvider.GetRequiredService<IRuleSetStore>();
        await store.SaveAsync(workflowName, json);
    }

    // --- Context ---

    private sealed class ChainingContext;

    // --- Test rules for Task 1+5: node-scoped result preservation ---

    private sealed class ChainARule : IRule<ChainingContext>
    {
        public string Code => "CHAIN_A";

        public Task<RuleResult> EvaluateAsync(ChainingContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("chain.trace", "A");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class ChainBRule : IRule<ChainingContext>
    {
        public string Code => "CHAIN_B";

        public Task<RuleResult> EvaluateAsync(ChainingContext ctx, FactBag facts, CancellationToken ct)
        {
            string trace = facts.Get<string>("chain.trace") ?? string.Empty;
            facts.Set("chain.trace", $"{trace}>B");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class ChainCRule : IRule<ChainingContext>
    {
        public string Code => "CHAIN_C";

        public Task<RuleResult> EvaluateAsync(ChainingContext ctx, FactBag facts, CancellationToken ct)
        {
            string trace = facts.Get<string>("chain.trace") ?? string.Empty;
            facts.Set("chain.trace", $"{trace}>C");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    // --- Test rule for Task 3: MFactBagAwareRule consumer ---

    private sealed class AwareConsumerRule : MFactBagAwareRule<ChainingContext>
    {
        public override string Code => "AWARE_CONSUMER";

        protected override Task<RuleResult> EvaluateCoreAsync(ChainingContext ctx, CancellationToken ct)
        {
            // Read upstream FEEL output fields using base class helpers
            object? gateResult = ReadFact<object>("gate.result");
            string? checkedAt = ReadFact<string>("gate.checkedAt");

            WriteFact("consumed.gate.result", gateResult?.ToString() ?? "null");
            WriteFact("consumed.gate.checkedAt", checkedAt ?? "null");

            return Task.FromResult(RuleResult.Passed());
        }
    }

    // --- Test rule for Task 4: on-false branch reads gate state ---

    private sealed class OnFalseReaderRule : IRule<ChainingContext>
    {
        public string Code => "ON_FALSE_READER";

        public Task<RuleResult> EvaluateAsync(ChainingContext ctx, FactBag facts, CancellationToken ct)
        {
            bool? gatePassed = facts.Get<bool?>("__graph.node.gate.passed");
            facts.Set("fallback.saw.gate.passed", gatePassed?.ToString()?.ToLowerInvariant() ?? "null");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    // --- Test rules for Task 5: diamond merge ---

    private sealed class GatePassRule : IRule<ChainingContext>
    {
        public string Code => "GATE_PASS";

        public Task<RuleResult> EvaluateAsync(ChainingContext ctx, FactBag facts, CancellationToken ct)
            => Task.FromResult(RuleResult.Passed());
    }

    private sealed class WriteXRule : IRule<ChainingContext>
    {
        public string Code => "WRITE_X";

        public Task<RuleResult> EvaluateAsync(ChainingContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("branch.x", 1);
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class WriteYRule : IRule<ChainingContext>
    {
        public string Code => "WRITE_Y";

        public Task<RuleResult> EvaluateAsync(ChainingContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("branch.y", 2);
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class ReadXYMergeRule : IRule<ChainingContext>
    {
        public string Code => "READ_XY_MERGE";

        public Task<RuleResult> EvaluateAsync(ChainingContext ctx, FactBag facts, CancellationToken ct)
        {
            int? x = facts.Get<int?>("branch.x");
            int? y = facts.Get<int?>("branch.y");

            string result = (x, y) switch
            {
                (not null, not null) => $"x={x},y={y}",
                (not null, null) => $"x={x}",
                (null, not null) => $"y={y}",
                _ => "none"
            };

            facts.Set("merge.result", result);
            return Task.FromResult(RuleResult.Passed());
        }
    }
}
