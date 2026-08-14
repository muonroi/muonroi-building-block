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
    public async Task ExecuteAsync_SubFlowShouldApplyInputTransforms_AndUseChildPathWhenParentPathEmpty()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "TransformChildFlow", """
        {
          "workflowName": "TransformChildFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "child-compiled", "type": "action", "ruleCode": "CHILD_TRANSFORM", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "child-compiled" },
              { "id": "e2", "source": "child-compiled", "target": "end" }
            ]
          }
        }
        """);
        await SaveWorkflowAsync(provider, "ParentTransformFlow", """
        {
          "workflowName": "ParentTransformFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              {
                "id": "subflow",
                "type": "sub-flow",
                "data": {
                  "subFlowConfig": {
                    "targetFlowCode": "TransformChildFlow",
                    "inputMappings": [
                      { "sourcePath": "Amount", "targetPath": "InputAmount", "transformExpression": "number(amount)" },
                      { "sourcePath": "ShouldApprove", "targetPath": "ApprovalFlag", "transformExpression": "boolean(flag)" }
                    ],
                    "outputMappings": [
                      { "childPath": "TransformEcho", "parentPath": "", "exposeToParent": true }
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

        FactBag facts = await service.ExecuteAsync("ParentTransformFlow", new TransformParentContext { Amount = "12.5", ShouldApprove = "true" });

        facts.Get<string>("TransformEcho").Should().Be("amount:12.5|approved:True");
    }

    [Fact]
    public async Task ExecuteWithResultAsync_SubFlowMissingChildWorkflow_ShouldReturnFailure()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "ParentMissingChildFlow", """
        {
          "workflowName": "ParentMissingChildFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              {
                "id": "subflow",
                "type": "sub-flow",
                "data": {
                  "subFlowConfig": {
                    "targetFlowCode": "MissingChildFlow",
                    "inputMappings": [],
                    "outputMappings": []
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

        OrchestratorResult result = await service.ExecuteWithResultAsync("ParentMissingChildFlow", new ParentContext { Value = "ABC123" });

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("MissingChildFlow", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task ExecuteAsync_ShouldWriteConditionOutputFactsWhenFeelConditionPasses()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "FeelOutputFlow", """
        {
          "workflowName": "FeelOutputFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              {
                "id": "cond",
                "type": "condition",
                "data": {
                  "expression": { "language": "feel", "body": "true" },
                  "outputFields": [
                    { "path": "hello", "valueExpression": "\"world\"", "dataType": "string" }
                  ]
                }
              },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "cond", "edgeType": "always" },
              { "id": "e2", "source": "cond", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("FeelOutputFlow", new GraphContext());

        facts.Get<string>("hello").Should().Be("world");
        facts.Get<Dictionary<string, object?>>("result")?["isPass"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFollowOnFalseBranchWithoutHaltingWorkflow()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "OnFalseFlow", """
        {
          "workflowName": "OnFalseFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "failure", "type": "condition", "ruleCode": "FAIL_WITH_FACT", "data": {} },
              { "id": "recovery", "type": "action", "ruleCode": "RECOVER_FALSE", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "failure", "edgeType": "always" },
              { "id": "e2", "source": "failure", "target": "recovery", "edgeType": "on-false" },
              { "id": "e3", "source": "recovery", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("OnFalseFlow", new GraphContext());

        facts.Get<string>("RecoveryPath").Should().Be("on-false");
        facts.Get<Dictionary<string, object?>>("result")?["isPass"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFollowOnErrorBranchWithoutHaltingWorkflow()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "OnErrorFlow", """
        {
          "workflowName": "OnErrorFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "throws", "type": "condition", "ruleCode": "THROW_RULE", "data": {} },
              { "id": "recovery", "type": "action", "ruleCode": "RECOVER_ERROR", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "throws", "edgeType": "always" },
              { "id": "e2", "source": "throws", "target": "recovery", "edgeType": "on-error" },
              { "id": "e3", "source": "recovery", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("OnErrorFlow", new GraphContext());

        facts.Get<string>("RecoveryPath").Should().Be("on-error");
        facts.Get<Dictionary<string, object?>>("result")?["errorCode"].Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipOnTrueBranchWhenUpstreamFails()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "OnTrueSkipFlow", """
        {
          "workflowName": "OnTrueSkipFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "failure", "type": "condition", "ruleCode": "FAIL_WITH_FACT", "data": {} },
              { "id": "success", "type": "action", "ruleCode": "MARK_SUCCESS", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "failure", "edgeType": "always" },
              { "id": "e2", "source": "failure", "target": "success", "edgeType": "on-true" },
              { "id": "e3", "source": "success", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        OrchestratorResult result = await service.ExecuteWithResultAsync("OnTrueSkipFlow", new GraphContext());

        result.IsSuccess.Should().BeFalse();
        result.Facts.Get<string>("RecoveryPath").Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinueThroughAlwaysBranchAfterFailure()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "AlwaysFailureFlow", """
        {
          "workflowName": "AlwaysFailureFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "failure", "type": "condition", "ruleCode": "FAIL_WITH_FACT", "data": {} },
              { "id": "always", "type": "action", "ruleCode": "RUN_ALWAYS", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "failure", "edgeType": "always" },
              { "id": "e2", "source": "failure", "target": "always", "edgeType": "always" },
              { "id": "e3", "source": "always", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("AlwaysFailureFlow", new GraphContext());

        facts.Get<string>("RecoveryPath").Should().Be("always");
        facts.Get<Dictionary<string, object?>>("result")?["isPass"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinueThroughAlwaysBranchAfterError()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "AlwaysErrorFlow", """
        {
          "workflowName": "AlwaysErrorFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "throws", "type": "condition", "ruleCode": "THROW_RULE", "data": {} },
              { "id": "always", "type": "action", "ruleCode": "RUN_ALWAYS", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "throws", "edgeType": "always" },
              { "id": "e2", "source": "throws", "target": "always", "edgeType": "always" },
              { "id": "e3", "source": "always", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("AlwaysErrorFlow", new GraphContext());

        facts.Get<string>("RecoveryPath").Should().Be("always");
        facts.Get<Dictionary<string, object?>>("result")?["errorCode"].Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowDiamondBranchesToRejoinAtSharedNode()
    {
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "DiamondFlow", """
        {
          "workflowName": "DiamondFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "seed", "type": "action", "ruleCode": "SEED_GRAPH", "data": {} },
              { "id": "left", "type": "action", "ruleCode": "MARK_LEFT", "data": {} },
              { "id": "right", "type": "action", "ruleCode": "MARK_RIGHT", "data": {} },
              { "id": "join", "type": "action", "ruleCode": "MARK_JOIN", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "seed", "edgeType": "always" },
              { "id": "e2", "source": "seed", "target": "left", "edgeType": "always" },
              { "id": "e3", "source": "seed", "target": "right", "edgeType": "always" },
              { "id": "e4", "source": "left", "target": "join", "edgeType": "always" },
              { "id": "e5", "source": "right", "target": "join", "edgeType": "always" },
              { "id": "e6", "source": "join", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("DiamondFlow", new GraphContext());

        facts.Get<string>("BranchTrace").Should().Be("seed>left>right>join");
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
        services.AddScoped<IRule<GraphContext>, RecoverFalseRule>();
        services.AddScoped<IRule<GraphContext>, ThrowRule>();
        services.AddScoped<IRule<GraphContext>, RecoverErrorRule>();
        services.AddScoped<IRule<GraphContext>, MarkSuccessRule>();
        services.AddScoped<IRule<GraphContext>, RunAlwaysRule>();
        services.AddScoped<IRule<GraphContext>, SeedGraphRule>();
        services.AddScoped<IRule<GraphContext>, MarkLeftRule>();
        services.AddScoped<IRule<GraphContext>, MarkRightRule>();
        services.AddScoped<IRule<GraphContext>, MarkJoinRule>();
        services.AddScoped<IRule<OrderedContext>, RequiresSeedRule>();
        services.AddScoped<IRule<OrderedContext>, SeedRule>();
        services.AddScoped<IRule<ChildContext>, ChildCompiledRule>();
        services.AddScoped<IRule<TransformChildContext>, TransformChildRule>();
        services.AddRuleEngine<TraceTestContext>();
        services.AddScoped<IRule<TraceTestContext>, CaptureInputRule>();
        services.AddScoped<IRule<TraceTestContext>, ProduceFactRule>();
        services.AddScoped<IRule<TraceTestContext>, ConsumeFactRule>();
        services.AddScoped<IRule<TraceTestContext>, ProduceLeftRule>();
        services.AddScoped<IRule<TraceTestContext>, ProduceRightRule>();
        services.AddScoped<IRule<TraceTestContext>, JoinFactsRule>();
        services.AddScoped<IRule<TraceTestContext>, NoopRule>();
        services.AddScoped<IRule<TraceTestContext>, TraceFailRule>();

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

    private sealed class TransformParentContext
    {
        public string Amount { get; set; } = string.Empty;
        public string ShouldApprove { get; set; } = string.Empty;
    }

    private sealed class TransformChildContext
    {
        public decimal InputAmount { get; set; }
        public bool ApprovalFlag { get; set; }
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

    private sealed class RecoverFalseRule : IRule<GraphContext>
    {
        public string Code => "RECOVER_FALSE";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("RecoveryPath", "on-false");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class ThrowRule : IRule<GraphContext>
    {
        public string Code => "THROW_RULE";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class RecoverErrorRule : IRule<GraphContext>
    {
        public string Code => "RECOVER_ERROR";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("RecoveryPath", "on-error");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class MarkSuccessRule : IRule<GraphContext>
    {
        public string Code => "MARK_SUCCESS";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("RecoveryPath", "on-true");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class RunAlwaysRule : IRule<GraphContext>
    {
        public string Code => "RUN_ALWAYS";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("RecoveryPath", "always");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class SeedGraphRule : IRule<GraphContext>
    {
        public string Code => "SEED_GRAPH";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("BranchTrace", "seed");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class MarkLeftRule : IRule<GraphContext>
    {
        public string Code => "MARK_LEFT";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            string prefix = facts.Get<string>("BranchTrace") ?? string.Empty;
            facts.Set("BranchTrace", $"{prefix}>left");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class MarkRightRule : IRule<GraphContext>
    {
        public string Code => "MARK_RIGHT";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            string prefix = facts.Get<string>("BranchTrace") ?? string.Empty;
            facts.Set("BranchTrace", $"{prefix}>right");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class MarkJoinRule : IRule<GraphContext>
    {
        public string Code => "MARK_JOIN";

        public Task<RuleResult> EvaluateAsync(GraphContext ctx, FactBag facts, CancellationToken ct)
        {
            string prefix = facts.Get<string>("BranchTrace") ?? string.Empty;
            facts.Set("BranchTrace", $"{prefix}>join");
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

    private sealed class TransformChildRule : IRule<TransformChildContext>
    {
        public string Code => "CHILD_TRANSFORM";

        public Task<RuleResult> EvaluateAsync(TransformChildContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("TransformEcho", $"amount:{ctx.InputAmount}|approved:{ctx.ApprovalFlag}");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    // ── Edge-scoped input capture tests ────────────────────────────────────────

    [Fact]
    public async Task EdgeScopedInput_StartNode_ShouldCaptureInitialNonInternalFacts()
    {
        // Start node (trigger has IncomingEdges.Count == 0) should store __trace.initial.input
        // containing only the non-internal keys present before execution.
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "StartNodeInputFlow", """
        {
          "workflowName": "StartNodeInputFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "capture", "type": "action", "ruleCode": "CAPTURE_INPUT", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "capture", "edgeType": "always" },
              { "id": "e2", "source": "capture", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("StartNodeInputFlow", new TraceTestContext { InitialValue = "hello" });

        // The trigger node is a start node (no incoming edges).
        // After execution, __trace.initial.input should exist and contain only non-internal keys.
        Dictionary<string, object?>? initialInput = facts.Get<Dictionary<string, object?>>("__trace.initial.input");
        initialInput.Should().NotBeNull("start node should write __trace.initial.input");
        initialInput!.Keys.Should().NotContain(k =>
            k.StartsWith("__graph.", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("__trace.", StringComparison.OrdinalIgnoreCase),
            "initial input should only contain non-internal keys");
    }

    [Fact]
    public async Task EdgeScopedInput_SinglePredecessorNode_ShouldContainOnlyPredecessorOutput()
    {
        // A node with one incoming edge should have its __trace.node.{id}.input
        // set to ONLY the predecessor's __trace.node.{sourceId}.output, not the full FactBag.
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "SinglePredInputFlow", """
        {
          "workflowName": "SinglePredInputFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "producer", "type": "action", "ruleCode": "PRODUCE_FACT", "data": {} },
              { "id": "consumer", "type": "action", "ruleCode": "CONSUME_FACT", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "producer", "edgeType": "always" },
              { "id": "e2", "source": "producer", "target": "consumer", "edgeType": "always" },
              { "id": "e3", "source": "consumer", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("SinglePredInputFlow", new TraceTestContext());

        // consumer's input should be scoped to producer's output (only "ProducedFact")
        Dictionary<string, object?>? consumerInput = facts.Get<Dictionary<string, object?>>($"__trace.node.consumer.input");
        consumerInput.Should().NotBeNull("consumer node should have edge-scoped input snapshot");

        // The producer outputs "ProducedFact". The full FactBag also contains other keys from context.
        // Edge-scoped input should NOT contain keys that were in FactBag before producer ran but not in producer's output.
        consumerInput!.Should().ContainKey("ProducedFact",
            "consumer's input should contain what producer produced as output");
    }

    [Fact]
    public async Task EdgeScopedInput_MultiPredecessorMerge_BothActive_ShouldUnionBothOutputs()
    {
        // Diamond pattern: seed → left + right → join
        // join has two incoming edges (left and right), both active ("always").
        // join's input should be the union of left's output and right's output.
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "MultiPredMergeInputFlow", """
        {
          "workflowName": "MultiPredMergeInputFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "left", "type": "action", "ruleCode": "PRODUCE_LEFT", "data": {} },
              { "id": "right", "type": "action", "ruleCode": "PRODUCE_RIGHT", "data": {} },
              { "id": "join", "type": "action", "ruleCode": "JOIN_FACTS", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "left", "edgeType": "always" },
              { "id": "e2", "source": "trigger", "target": "right", "edgeType": "always" },
              { "id": "e3", "source": "left", "target": "join", "edgeType": "always" },
              { "id": "e4", "source": "right", "target": "join", "edgeType": "always" },
              { "id": "e5", "source": "join", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("MultiPredMergeInputFlow", new TraceTestContext());

        // join's input should contain BOTH LeftFact and RightFact (merged from both active predecessors)
        Dictionary<string, object?>? joinInput = facts.Get<Dictionary<string, object?>>($"__trace.node.join.input");
        joinInput.Should().NotBeNull("join node should have edge-scoped input snapshot");
        joinInput!.Should().ContainKey("LeftFact", "join input should contain left predecessor's output");
        joinInput!.Should().ContainKey("RightFact", "join input should contain right predecessor's output");
    }

    [Fact]
    public async Task EdgeScopedInput_MultiPredecessorMerge_OneInactive_ShouldOnlyIncludeActiveOutput()
    {
        // A node with two incoming edges (on-true and on-false) where upstream fails:
        // - on-true edge (from "cond" when passed) → inactive because cond fails
        // - on-false edge (from "cond" when not passed) → active because cond fails
        // After-node input should only contain the on-false predecessor's output.
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "PartialActiveEdgeFlow", """
        {
          "workflowName": "PartialActiveEdgeFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "cond", "type": "condition", "ruleCode": "TRACE_FAIL", "data": {} },
              { "id": "on-true-action", "type": "action", "ruleCode": "PRODUCE_LEFT", "data": {} },
              { "id": "on-false-action", "type": "action", "ruleCode": "PRODUCE_RIGHT", "data": {} },
              { "id": "after", "type": "action", "ruleCode": "JOIN_FACTS", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "cond", "edgeType": "always" },
              { "id": "e2", "source": "cond", "target": "on-true-action", "edgeType": "on-true" },
              { "id": "e3", "source": "cond", "target": "on-false-action", "edgeType": "on-false" },
              { "id": "e4", "source": "on-true-action", "target": "after", "edgeType": "always" },
              { "id": "e5", "source": "on-false-action", "target": "after", "edgeType": "always" },
              { "id": "e6", "source": "after", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("PartialActiveEdgeFlow", new TraceTestContext());

        // cond fails → on-false-action runs, on-true-action is skipped.
        // "after" node's input should contain only RightFact (from on-false-action),
        // NOT LeftFact (on-true-action was skipped so its output trace may not exist or is not active).
        Dictionary<string, object?>? afterInput = facts.Get<Dictionary<string, object?>>($"__trace.node.after.input");
        afterInput.Should().NotBeNull("after node should have edge-scoped input snapshot");
        afterInput!.Should().ContainKey("RightFact", "after input should contain the active predecessor's output");
    }

    [Fact]
    public async Task EdgeScopedInput_PredecessorWithNoOutputTrace_ShouldProduceEmptyInput()
    {
        // A node that doesn't write any new facts produces an empty output snapshot.
        // The successor's input should be an empty dict (graceful fallback).
        await using ServiceProvider provider = BuildProvider();
        await SaveWorkflowAsync(provider, "EmptyOutputFlow", """
        {
          "workflowName": "EmptyOutputFlow",
          "flowGraph": {
            "nodes": [
              { "id": "trigger", "type": "trigger", "data": {} },
              { "id": "noop", "type": "action", "ruleCode": "NOOP_RULE", "data": {} },
              { "id": "after", "type": "action", "ruleCode": "CONSUME_FACT", "data": {} },
              { "id": "end", "type": "end", "data": {} }
            ],
            "edges": [
              { "id": "e1", "source": "trigger", "target": "noop", "edgeType": "always" },
              { "id": "e2", "source": "noop", "target": "after", "edgeType": "always" },
              { "id": "e3", "source": "after", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """);

        using IServiceScope scope = provider.CreateScope();
        RulesEngineService service = scope.ServiceProvider.GetRequiredService<RulesEngineService>();

        FactBag facts = await service.ExecuteAsync("EmptyOutputFlow", new TraceTestContext());

        // after's input should be scoped to the noop node's output.
        // noop writes nothing user-visible (no business facts), but the adapter writes a "result" status key.
        // The edge-scoped input should NOT contain any facts that existed in FactBag before noop ran
        // (e.g. keys present from the initial request context).
        Dictionary<string, object?>? afterInput = facts.Get<Dictionary<string, object?>>($"__trace.node.after.input");
        afterInput.Should().NotBeNull("after node should have an edge-scoped input snapshot");
        // The input is sourced from noop's __trace.node.noop.output, NOT the full FactBag.
        // noop's output only contains what the adapter wrote (e.g. "result" status) — no user-provided initial facts.
        afterInput!.Keys.Should().NotContain("InitialValue",
            "InitialValue was in the full FactBag but not in noop's edge-scoped output");
    }

    // ── Additional rules for edge-scoped input tests ───────────────────────────

    private sealed class TraceTestContext
    {
        public string InitialValue { get; set; } = string.Empty;
    }

    private sealed class TraceFailRule : IRule<TraceTestContext>
    {
        public string Code => "TRACE_FAIL";

        public Task<RuleResult> EvaluateAsync(TraceTestContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("FailFact", "fail");
            return Task.FromResult(RuleResult.Failure("forced failure"));
        }
    }

    private sealed class CaptureInputRule : IRule<TraceTestContext>
    {
        public string Code => "CAPTURE_INPUT";

        public Task<RuleResult> EvaluateAsync(TraceTestContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("CapturedValue", ctx.InitialValue);
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class ProduceFactRule : IRule<TraceTestContext>
    {
        public string Code => "PRODUCE_FACT";

        public Task<RuleResult> EvaluateAsync(TraceTestContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("ProducedFact", "produced-value");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class ConsumeFactRule : IRule<TraceTestContext>
    {
        public string Code => "CONSUME_FACT";

        public Task<RuleResult> EvaluateAsync(TraceTestContext ctx, FactBag facts, CancellationToken ct)
        {
            // Reads what was produced; doesn't add new facts
            facts.Set("ConsumedValue", facts.Get<string>("ProducedFact") ?? "nothing");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class ProduceLeftRule : IRule<TraceTestContext>
    {
        public string Code => "PRODUCE_LEFT";

        public Task<RuleResult> EvaluateAsync(TraceTestContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("LeftFact", "left-value");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class ProduceRightRule : IRule<TraceTestContext>
    {
        public string Code => "PRODUCE_RIGHT";

        public Task<RuleResult> EvaluateAsync(TraceTestContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("RightFact", "right-value");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class JoinFactsRule : IRule<TraceTestContext>
    {
        public string Code => "JOIN_FACTS";

        public Task<RuleResult> EvaluateAsync(TraceTestContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("Joined", "done");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class NoopRule : IRule<TraceTestContext>
    {
        public string Code => "NOOP_RULE";

        public Task<RuleResult> EvaluateAsync(TraceTestContext ctx, FactBag facts, CancellationToken ct)
        {
            // Intentionally writes nothing to FactBag
            return Task.FromResult(RuleResult.Passed());
        }
    }
}
