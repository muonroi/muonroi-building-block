using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Logging;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleGraphRuntimeAdapterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "muonroi-rule-runtime-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExecuteAsync_ShouldPreferEmbeddedFlowGraphOverRulesArray()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "EnvelopeFlow", """
        {
          "workflowName": "EnvelopeFlow",
          "rules": ["UNKNOWN_RULE"],
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "compiled", "type": "condition", "ruleCode": "GRAPH_COMPILED", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "compiled" },
              { "id": "e2", "source": "compiled", "target": "end" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("EnvelopeFlow", new GraphContext());

        facts.Get<string>("GraphMarker").Should().Be("compiled");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHonorGraphOrderOverrideForCompiledRules()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "OrderedFlow", """
        {
          "workflowName": "OrderedFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "seed", "type": "action", "ruleCode": "SEED_RULE", "data": {} },
              { "id": "requires", "type": "condition", "ruleCode": "REQUIRES_SEED", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "seed" },
              { "id": "e2", "source": "seed", "target": "requires" },
              { "id": "e3", "source": "requires", "target": "end" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("OrderedFlow", new OrderedContext());

        facts.Get<string>("Sequence").Should().Be("seed>requires");
    }

    [Fact]
    public async Task ExecuteAsync_SubFlowShouldExecuteCompiledChildRuleViaFactBagAdapter()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "ChildFlow", """
        {
          "workflowName": "ChildFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "child-compiled", "type": "action", "ruleCode": "CHILD_COMPILED", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "child-compiled" },
              { "id": "e2", "source": "child-compiled", "target": "end" }
            ]
          }
        }
        """);
        await SaveWorkflowAsync(provider, "ParentFlow", """
        {
          "workflowName": "ParentFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              {
                "id": "subflow",
                "type": "sub-flow",
                "data": {
                  "subFlowConfig": {
                    "targetFlowCode": "ChildFlow",
                    "inputMappings": [
                      { "sourcePath": "Value", "targetPath": "Input" }
                    ],
                    "outputMappings": [
                      { "childPath": "ChildEcho", "parentPath": "MergedValue", "exposeToParent": true }
                    ]
                  }
                }
              },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "subflow" },
              { "id": "e2", "source": "subflow", "target": "end" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("ParentFlow", new ParentContext { Value = "ABC123" });

        facts.Get<string>("MergedValue").Should().Be("child:ABC123");
    }

    [Fact]
    public async Task ExecuteWithResultAsync_ShouldPreserveFactsOnFailure()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "FailureFlow", """
        {
          "workflowName": "FailureFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "failure", "type": "condition", "ruleCode": "FAIL_WITH_FACT", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "failure" },
              { "id": "e2", "source": "failure", "target": "end" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        OrchestratorResult result = await service.ExecuteWithResultAsync("FailureFlow", new GraphContext());

        result.IsSuccess.Should().BeFalse();
        result.Facts.Get<string>("ErrorMessage").Should().Be("compiled failure");
        result.Errors.Should().ContainSingle(error => error.Contains("compiled failure", StringComparison.OrdinalIgnoreCase));
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
        services.AddRuleEngine<GraphContext>();
        services.AddRuleEngine<OrderedContext>();
        services.AddRuleEngine<ParentContext>();
        services.AddRuleEngine<ChildContext>();
        services.AddRuleEngineStore(configuration);

        services.AddScoped<IRule<GraphContext>, GraphCompiledRule>();
        services.AddScoped<IRule<GraphContext>, FailWithFactRule>();
        services.AddScoped<IRule<OrderedContext>, RequiresSeedRule>();
        services.AddScoped<IRule<OrderedContext>, SeedRule>();
        services.AddScoped<IRule<ChildContext>, ChildCompiledRule>();

        return services.BuildServiceProvider();
    }

    private static async Task SaveWorkflowAsync(ServiceProvider provider, string workflowName, string json)
    {
        using IServiceScope scope = provider.CreateScope();
        IRuleSetStore store = scope.ServiceProvider.GetRequiredService<IRuleSetStore>();
        await store.SaveAsync(workflowName, json);
    }

    private sealed class GraphContext;

    private sealed class OrderedContext;

    private sealed class ParentContext
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class ChildContext
    {
        public string Input { get; set; } = string.Empty;
    }

    private sealed class GraphCompiledRule : IRule<GraphContext>
    {
        public string Code => "GRAPH_COMPILED";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("GraphMarker", "compiled");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class FailWithFactRule : IRule<GraphContext>
    {
        public string Code => "FAIL_WITH_FACT";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("ErrorMessage", "compiled failure");
            return Task.FromResult(RuleResult.Failure("compiled failure"));
        }
    }

    private sealed class SeedRule : IRule<OrderedContext>
    {
        public string Code => "SEED_RULE";
        public int Order => 100;

        public Task<RuleResult> EvaluateAsync(OrderedContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("SeedReady", true);
            facts.Set("Sequence", "seed");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class RequiresSeedRule : IRule<OrderedContext>
    {
        public string Code => "REQUIRES_SEED";
        public int Order => 0;

        public Task<RuleResult> EvaluateAsync(OrderedContext ctx, FactBag facts, CancellationToken ct)
        {
            bool seeded = facts.Get<bool?>("SeedReady") == true;
            if (!seeded)
            {
                return Task.FromResult(RuleResult.Failure("seed missing"));
            }

            string prefix = facts.Get<string>("Sequence") ?? string.Empty;
            facts.Set("Sequence", $"{prefix}>requires");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class ChildCompiledRule : IRule<ChildContext>
    {
        public string Code => "CHILD_COMPILED";

        public Task<RuleResult> EvaluateAsync(ChildContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("ChildEcho", $"child:{ctx.Input}");
            return Task.FromResult(RuleResult.Passed());
        }
    }
}
