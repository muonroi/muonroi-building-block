
using RulesEngine.Models;

namespace Muonroi.BuildingBlock.Test;
[Collection("NonParallel")]
public class RulesEngineServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Stores_ActionOutputs_In_FactBag()
    {
        TenantContext.CurrentTenantId = null;
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);
        const string json = """
                            [
                              {
                                "WorkflowName": "TestWorkflow",
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
        await service.SaveRuleSetAsync("TestWorkflow", json);
        FactBag bag = await service.ExecuteAsync("TestWorkflow", 3);
        Assert.Equal(6, bag.Get<int>("Double"));
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task ExecuteAsync_Uses_Active_Version()
    {
        TenantContext.CurrentTenantId = "t1";
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(dir);
        RulesEngineService service = new(store);
        const string jsonV1 = """
                              [
                                {
                                  "WorkflowName": "TestWorkflow",
                                  "Rules": [
                                    {
                                      "RuleName": "Calc",
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
        const string jsonV2 = """
                              [
                                {
                                  "WorkflowName": "TestWorkflow",
                                  "Rules": [
                                    {
                                      "RuleName": "Calc",
                                      "RuleExpressionType": "LambdaExpression",
                                      "Expression": "input1.value > 0",
                                      "Actions": {
                                        "OnSuccess": {
                                          "Name": "OutputExpression",
                                          "Context": {
                                            "expression": "input1.value * 3"
                                          }
                                        }
                                      }
                                    }
                                  ]
                                }
                              ]
                              """;
        await service.SaveRuleSetAsync("TestWorkflow", jsonV1);
        await service.SaveRuleSetAsync("TestWorkflow", jsonV2);
        FactBag bag = await service.ExecuteAsync("TestWorkflow", 2);
        Assert.Equal(6, bag.Get<int>("Calc"));
        await service.SetActiveVersionAsync("TestWorkflow", 1);
        bag = await service.ExecuteAsync("TestWorkflow", 2);
        Assert.Equal(4, bag.Get<int>("Calc"));
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task FileRuleSetStore_Is_Tenant_Isolated()
    {
        TenantContext.CurrentTenantId = "t1";
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(dir);
        await store.SaveAsync("WF", "{}");
        TenantContext.CurrentTenantId = "t2";
        string? json = await store.GetAsync("WF");
        Assert.Null(json);
        TenantContext.CurrentTenantId = "t1";
        json = await store.GetAsync("WF");
        Assert.NotNull(json);
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task ExecuteAsync_Supports_Custom_Type_Aliases()
    {
        TenantContext.CurrentTenantId = null;
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(dir);
        ReSettings settings = new()
        {
            CustomTypes = [typeof(TestHelpers)]
        };
        RulesEngineService service = new(store, settings);
        const string json = """
                            [
                              {
                                "WorkflowName": "WF",
                                "Rules": [
                                  {
                                    "RuleName": "Check",
                                    "RuleExpressionType": "LambdaExpression",
                                    "Expression": "TestHelpers.IsEven(input1.value)",
                                    "Actions": {
                                      "OnSuccess": {
                                        "Name": "OutputExpression",
                                        "Context": {
                                          "expression": "input1.value"
                                        }
                                      }
                                    }
                                  }
                                ]
                              }
                            ]
                            """;
        await service.SaveRuleSetAsync("WF", json);
        FactBag bag = await service.ExecuteAsync("WF", 4);
        Assert.Equal(4, bag.Get<int>("Check"));
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task ExecuteAsync_CodeWorkflow_ResolvesRulesWithConstructorDependencies()
    {
        TenantContext.CurrentTenantId = null;
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(dir);

        ServiceCollection services = new();
        services.AddScoped<ScopedRuleDependency>();
        services.AddScoped<IRule<ScopedRuleContext>, ScopedRule>();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        RulesEngineService service = new(store, serviceProvider: scope.ServiceProvider);

        const string json = """
                            [
                              {
                                "WorkflowName": "WF",
                                "Rules": [ "ScopedRuleCode" ]
                              }
                            ]
                            """;
        await service.SaveRuleSetAsync("WF", json);

        ScopedRuleContext context = new();
        FactBag bag = await service.ExecuteAsync("WF", context);

        Assert.Equal("from-di", context.Result);
        Assert.Equal("from-di", bag.Get<string>("Result"));
        TenantContext.CurrentTenantId = null;
    }

    private static class TestHelpers
    {
        public static bool IsEven(int value)
        {
            return value % 2 == 0;
        }
    }

    private sealed class ScopedRuleContext
    {
        public string? Result { get; set; }
    }

    private sealed class ScopedRuleDependency
    {
        public static string Value => "from-di";
    }

    private sealed class ScopedRule : IRule<ScopedRuleContext>
    {
        public string Code => "ScopedRuleCode";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(ScopedRuleContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(ScopedRuleContext context, CancellationToken cancellationToken = default)
        {
            context.Result = ScopedRuleDependency.Value;
            return Task.CompletedTask;
        }
    }
}
