namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class RuleEngineSampleVerificationTests
{
    [Fact]
    public async Task TypedRuleEngine_RuntimeAddAndRemoveRule_Works()
    {
        RuleEngine<RuntimeTypedContext> engine = new();
        engine.AddRule(new TypedBaseRule());
        engine.AddRule(new TypedPlusFiveRule());

        RuntimeTypedContext first = new();
        await engine.ExecuteAsync(first, CancellationToken.None, System.Enum.GetValues<RuleType>());
        Assert.Equal(15, first.Result);

        bool removed = engine.RemoveRule("TypedPlusFive");
        Assert.True(removed);
        engine.AddRule(new TypedPlusTwentyRule());

        RuntimeTypedContext second = new();
        await engine.ExecuteAsync(second, CancellationToken.None, System.Enum.GetValues<RuleType>());
        Assert.Equal(30, second.Result);
    }

    [Fact]
    public async Task JsonWorkflow_RuntimeAddAndRemoveRuleByVersion_Works()
    {
        TenantContext.CurrentTenantId = "tenant-rule-sample";
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);

        const string v1 = """
                          [
                            {
                              "WorkflowName": "RuntimeWorkflow",
                              "Rules": [ "JsonBase", "JsonPlusFive" ]
                            }
                          ]
                          """;

        const string v2 = """
                          [
                            {
                              "WorkflowName": "RuntimeWorkflow",
                              "Rules": [ "JsonBase", "JsonPlusTwenty" ]
                            }
                          ]
                          """;

        await service.SaveRuleSetAsync("RuntimeWorkflow", v1);
        RuntimeJsonContext ctxV1 = new();
        FactBag bagV1 = await service.ExecuteAsync("RuntimeWorkflow", ctxV1);
        Assert.Equal(15, ctxV1.Result);
        Assert.Equal(15, bagV1.Get<int>("Result"));

        await service.SaveRuleSetAsync("RuntimeWorkflow", v2);
        RuntimeJsonContext ctxV2 = new();
        FactBag bagV2 = await service.ExecuteAsync("RuntimeWorkflow", ctxV2);
        Assert.Equal(30, ctxV2.Result);
        Assert.Equal(30, bagV2.Get<int>("Result"));

        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task JsonExpressionWorkflow_UsesJsonDefinitionWithoutHardcodedRuleClass()
    {
        TenantContext.CurrentTenantId = "tenant-expression-sample";
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);

        const string json = """
                            [
                              {
                                "WorkflowName": "ExpressionWorkflow",
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

        await service.SaveRuleSetAsync("ExpressionWorkflow", json);
        FactBag bag = await service.ExecuteAsync("ExpressionWorkflow", 7);

        Assert.Equal(14, bag.Get<int>("Double"));
        TenantContext.CurrentTenantId = null;
    }

    private sealed class RuntimeTypedContext
    {
        public int Result { get; set; }
    }

    private sealed class RuntimeJsonContext
    {
        public int Result { get; set; }
    }

    private sealed class TypedBaseRule : IRule<RuntimeTypedContext>
    {
        public string Code => "TypedBase";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(RuntimeTypedContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(RuntimeTypedContext context, CancellationToken cancellationToken = default)
        {
            context.Result = 10;
            return Task.CompletedTask;
        }
    }

    private sealed class TypedPlusFiveRule : IRule<RuntimeTypedContext>
    {
        public string Code => "TypedPlusFive";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => ["TypedBase"];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(RuntimeTypedContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(RuntimeTypedContext context, CancellationToken cancellationToken = default)
        {
            context.Result += 5;
            return Task.CompletedTask;
        }
    }

    private sealed class TypedPlusTwentyRule : IRule<RuntimeTypedContext>
    {
        public string Code => "TypedPlusTwenty";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => ["TypedBase"];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(RuntimeTypedContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(RuntimeTypedContext context, CancellationToken cancellationToken = default)
        {
            context.Result += 20;
            return Task.CompletedTask;
        }
    }

    private sealed class JsonBaseRule : IRule<RuntimeJsonContext>
    {
        public string Code => "JsonBase";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(RuntimeJsonContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(RuntimeJsonContext context, CancellationToken cancellationToken = default)
        {
            context.Result = 10;
            return Task.CompletedTask;
        }
    }

    private sealed class JsonPlusFiveRule : IRule<RuntimeJsonContext>
    {
        public string Code => "JsonPlusFive";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => ["JsonBase"];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(RuntimeJsonContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(RuntimeJsonContext context, CancellationToken cancellationToken = default)
        {
            context.Result += 5;
            return Task.CompletedTask;
        }
    }

    private sealed class JsonPlusTwentyRule : IRule<RuntimeJsonContext>
    {
        public string Code => "JsonPlusTwenty";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => ["JsonBase"];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(RuntimeJsonContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(RuntimeJsonContext context, CancellationToken cancellationToken = default)
        {
            context.Result += 20;
            return Task.CompletedTask;
        }
    }
}
