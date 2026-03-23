using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.Abstractions.Adapters;
using Muonroi.RuleEngine.Runtime.Adapters;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Runtime.Rules;
using RulesEngine.Models;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RulesEngineServiceInternalTests
{
    [Fact]
    public void ParseWorkflowDefinition_CodeWorkflowObject_ExtractsCodesAndExecutionMode()
    {
        const string json = """
                            {
                              "workflowName": "wf-code",
                              "ExecutionMode": "BestEffort",
                              "rules": [ "RULE_A", "RULE_B" ]
                            }
                            """;

        object result = InvokePrivateStatic(nameof(RulesEngineService), "ParseWorkflowDefinition", json);

        GetProperty<string[]>(result, "RuleCodes").Should().Equal("RULE_A", "RULE_B");
        GetProperty<ExecutionMode?>(result, "ExecutionMode").Should().Be(ExecutionMode.BestEffort);
        GetProperty<object[]>(result, "LegacyWorkflows").Should().BeNull();
    }

    [Fact]
    public void ParseWorkflowDefinition_LegacyWorkflowArray_ProducesLegacyWorkflows()
    {
        const string json = """
                            [
                              {
                                "WorkflowName": "Legacy",
                                "Rules": [
                                  {
                                    "RuleName": "Always",
                                    "Expression": "true"
                                  }
                                ]
                              }
                            ]
                            """;

        object result = InvokePrivateStatic(nameof(RulesEngineService), "ParseWorkflowDefinition", json);

        GetProperty<string[]>(result, "RuleCodes").Should().BeNull();
        GetProperty<object[]>(result, "LegacyWorkflows").Should().HaveCount(1);
    }

    [Fact]
    public void ParseWorkflowDefinition_InvalidJson_FallsBackToEmptyLegacyDefinition()
    {
        object result = InvokePrivateStatic(nameof(RulesEngineService), "ParseWorkflowDefinition", "not-json");

        GetProperty<string[]>(result, "RuleCodes").Should().BeNull();
        GetProperty<object[]>(result, "LegacyWorkflows").Should().BeEmpty();
        GetProperty<ExecutionMode?>(result, "ExecutionMode").Should().BeNull();
    }

    [Fact]
    public void ResolveContextType_CanResolveByFullNameAndSimpleName()
    {
        Type byFullName = (Type)InvokePrivateStatic(nameof(RulesEngineService), "ResolveContextType", typeof(InternalContext).FullName!);
        Type bySimpleName = (Type)InvokePrivateStatic(nameof(RulesEngineService), "ResolveContextType", nameof(InternalContext));

        byFullName.Should().Be(typeof(InternalContext));
        bySimpleName.Should().Be(typeof(InternalContext));
    }

    [Fact]
    public void ConvertJsonElement_ConvertsNestedObjectsAndArrays()
    {
        JsonElement element = JsonDocument.Parse("""
                                                 {
                                                   "amount": 12.5,
                                                   "active": true,
                                                   "items": [1, "two", false]
                                                 }
                                                 """).RootElement.Clone();

        object? converted = InvokePrivateStatic(nameof(RulesEngineService), "ConvertJsonElement", element);

        Dictionary<string, object?> dict = converted.Should().BeOfType<Dictionary<string, object?>>().Subject;
        dict["amount"].Should().Be(12.5m);
        dict["active"].Should().Be(true);
        dict["items"].Should().BeAssignableTo<List<object?>>();
    }

    [Fact]
    public void NormalizeJsonOutput_ConvertsJsonElementObjectAndArray()
    {
        JsonElement objectElement = JsonDocument.Parse("""{"code":"A1","value":5}""").RootElement.Clone();
        JsonElement arrayElement = JsonDocument.Parse("""[1,2,3]""").RootElement.Clone();

        object? normalizedObject = InvokePrivateStatic(nameof(RulesEngineService), "NormalizeJsonOutput", (object)objectElement);
        object? normalizedArray = InvokePrivateStatic(nameof(RulesEngineService), "NormalizeJsonOutput", (object)arrayElement);

        normalizedObject.Should().BeAssignableTo<Dictionary<string, object?>>();
        normalizedArray.Should().BeAssignableTo<List<object?>>();
    }

    [Fact]
    public void ParseWorkflowDefinition_PreservesCodes_AndIgnoresInvalidExecutionMode()
    {
        const string json = """
                            {
                              "workflowName": "wf-code",
                              "executionMode": "DoesNotExist",
                              "rules": [ "RULE_A", "RULE_A", "RULE_B", "" ]
                            }
                            """;

        object result = InvokePrivateStatic(nameof(RulesEngineService), "ParseWorkflowDefinition", json);

        GetProperty<string[]>(result, "RuleCodes").Should().Equal("RULE_A", "RULE_A", "RULE_B");
        GetProperty<ExecutionMode?>(result, "ExecutionMode").Should().BeNull();
    }

    [Fact]
    public void ResolveTenantId_ReturnsDefault_WhenExecutionContextIsBlank()
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext(
            tenantId: "   ",
            userId: null,
            username: null,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "tests"));
        RulesEngineService service = new(new InMemoryStore(), executionContextAccessor: accessor);

        string tenantId = (string)InvokePrivateInstance(service, "ResolveTenantId")!;

        tenantId.Should().Be("default");
    }

    [Fact]
    public void GetOrCreateWorkflowDefinition_ReturnsCachedDefinition_ForSameJson()
    {
        const string json = """{"workflowName":"wf-cache","rules":["RULE_A"]}""";

        object first = InvokePrivateStatic(nameof(RulesEngineService), "GetOrCreateWorkflowDefinition", "tenant-a", "wf-cache", json);
        object second = InvokePrivateStatic(nameof(RulesEngineService), "GetOrCreateWorkflowDefinition", "tenant-a", "wf-cache", json);

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateWorkflowDefinition_Reparses_WhenJsonChanges()
    {
        object first = InvokePrivateStatic(nameof(RulesEngineService), "GetOrCreateWorkflowDefinition", "tenant-b", "wf-cache-change", """{"workflowName":"wf-cache-change","rules":["RULE_A"]}""");
        object second = InvokePrivateStatic(nameof(RulesEngineService), "GetOrCreateWorkflowDefinition", "tenant-b", "wf-cache-change", """{"workflowName":"wf-cache-change","rules":["RULE_B"]}""");

        ReferenceEquals(first, second).Should().BeFalse();
        GetProperty<string[]>(second, "RuleCodes").Should().Equal("RULE_B");
    }

    [Fact]
    public void ResolveContextType_Throws_WhenTypeCannotBeFound()
    {
        Action act = () => InvokePrivateStatic(nameof(RulesEngineService), "ResolveContextType", "Missing.Context.Type");

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidDataException>()
            .WithMessage("*Cannot resolve contextType*");
    }

    [Fact]
    public void TryCreateRuleInstance_CanInstantiateRuleWithPrivateCtor()
    {
        RulesEngineService service = new(new InMemoryStore());

        object? instance = InvokePrivateInstance(service, "TryCreateRuleInstance", typeof(PrivateCtorRule));

        instance.Should().BeOfType<PrivateCtorRule>();
    }

    [Fact]
    public void ResolveRulesByCode_UsesReflectionFallbackForUnresolvedCodes()
    {
        RulesEngineService service = new(new InMemoryStore());

        object map = InvokePrivateGenericInstance(
            service,
            "ResolveRulesByCode",
            typeof(InternalContext),
            new object[] { new[] { "PRIVATE_CTOR_RULE" } });

        Dictionary<string, List<IRule<InternalContext>>> typed =
            map.Should().BeOfType<Dictionary<string, List<IRule<InternalContext>>>>().Subject;
        typed.Should().ContainKey("PRIVATE_CTOR_RULE");
        typed["PRIVATE_CTOR_RULE"].Should().ContainSingle(x => x.GetType() == typeof(PrivateCtorRule));
    }

    [Fact]
    public async Task NotifyRuleChangedAsync_UsesDefaultTenant_AndPublishesEvent()
    {
        RecordingRuntimeCache cache = new();
        RecordingNotifier notifier = new();
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(SystemExecutionContext.Empty);
        RulesEngineService service = new(new InMemoryStore(), runtimeCache: cache, notifier: notifier, executionContextAccessor: accessor);

        object?[] args = ["wf-default", RuleSetChangeTypes.Saved, null, CancellationToken.None];
        await (Task)InvokePrivateInstance(service, "NotifyRuleChangedAsync", args)!;

        cache.LastTenantId.Should().Be("default");
        cache.LastWorkflowName.Should().Be("wf-default");
        notifier.Events.Should().ContainSingle();
        notifier.Events[0].TenantId.Should().Be("default");
        notifier.Events[0].WorkflowName.Should().Be("wf-default");
    }

    [Fact]
    public async Task ExecuteCodeWorkflowBridgeAsync_Throws_WhenContextTypeMismatches()
    {
        RulesEngineService service = new(new InMemoryStore());
        MethodInfo method = typeof(RulesEngineService)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(x => x.Name == "ExecuteCodeWorkflowBridgeAsync" && x.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(InternalContext));

        Func<Task> act = () =>
        {
            _ = method.Invoke(
                service,
                new object?[] { new[] { "PRIVATE_CTOR_RULE" }, new { Value = 1 }, ExecutionMode.AllOrNothing, CancellationToken.None });
            return Task.CompletedTask;
        };

        TargetInvocationException ex = (await act.Should().ThrowAsync<TargetInvocationException>()).Which;
        ex.InnerException.Should().BeOfType<InvalidDataException>();
        ex.InnerException!.Message.Should().Contain("Dry-run context type mismatch");
    }

    [Fact]
    public async Task ExecuteLegacyWorkflowBridgeAsync_Throws_WhenContextTypeMismatches()
    {
        RulesEngineService service = new(new InMemoryStore());
        MethodInfo method = typeof(RulesEngineService)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(x => x.Name == "ExecuteLegacyWorkflowBridgeAsync" && x.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(InternalContext));

        Func<Task> act = () =>
        {
            _ = method.Invoke(
                service,
                new object?[] { "wf-legacy", Array.Empty<Workflow>(), new { Value = 1 } });
            return Task.CompletedTask;
        };

        TargetInvocationException ex = (await act.Should().ThrowAsync<TargetInvocationException>()).Which;
        ex.InnerException.Should().BeOfType<InvalidDataException>();
        ex.InnerException!.Message.Should().Contain("Legacy dry-run context type mismatch");
    }

    [Fact]
    public void TryCreateRuleInstance_FallsBack_WhenServiceProviderCannotConstruct()
    {
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        RulesEngineService service = new(new InMemoryStore(), serviceProvider: provider);

        object? instance = InvokePrivateInstance(service, "TryCreateRuleInstance", typeof(PrivateCtorRule));

        instance.Should().BeOfType<PrivateCtorRule>();
    }

    [Fact]
    public void LoadWorkflowJsonAsync_UsesStoreDirectly_WhenRuntimeCacheIsMissing()
    {
        RecordingStore store = new()
        {
            LatestJson = """{"workflowName":"wf-direct","rules":["RULE_A"]}"""
        };
        RulesEngineService service = new(store);

        object result = InvokePrivateTaskInstance(service, "LoadWorkflowJsonAsync", "wf-direct", CancellationToken.None)!;

        GetTupleItem<string>(result, "Item1").Should().Be("default");
        GetTupleItem<string>(result, "Item2").Should().Be(store.LatestJson);
        store.LastGetWorkflowName.Should().Be("wf-direct");
        store.LastGetVersion.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteSubFlowAsync_ReturnsFailure_WhenWorkflowMissing()
    {
        RulesEngineService service = new(new RecordingStore(), graphParser: new RuleGraphParser(new Muonroi.Core.Abstractions.SeedWorks.MJsonSerializeService()));

        SubFlowExecutionResult result = await service.ExecuteSubFlowAsync("wf-missing", new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteSubFlowAsync_ReturnsFailure_WhenWorkflowIsNotGraph()
    {
        RecordingStore store = new()
        {
            LatestJson = """{"workflowName":"wf-legacy","rules":["RULE_A"]}"""
        };
        RulesEngineService service = new(store, graphParser: new RuleGraphParser(new Muonroi.Core.Abstractions.SeedWorks.MJsonSerializeService()));

        SubFlowExecutionResult result = await service.ExecuteSubFlowAsync("wf-legacy", new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("not a flow graph");
    }

    [Fact]
    public async Task TryResolveCompiledRuleAsFactBag_ReturnsAdaptedRule_WhenFactoryExists()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IContextFactory<AdaptedContext>, AdaptedContextFactory>()
            .BuildServiceProvider();
        RulesEngineService service = new(new InMemoryStore(), serviceProvider: provider);
        RuleGraphEntry entry = new()
        {
            NodeId = "compiled-adapted",
            RuleCode = "FACTBAG_ADAPTED"
        };

        object? resolved = InvokePrivateInstance(service, "TryResolveCompiledRuleAsFactBag", entry);

        IRule<FactBagRuleContext> rule = resolved.Should().BeAssignableTo<IRule<FactBagRuleContext>>().Subject;
        FactBag facts = new();
        FactBagRuleContext context = new(new FactBag { ["Value"] = 3 });

        RuleResult result = await rule.EvaluateAsync(context, facts, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        facts.Get<int>("adapted").Should().Be(12);
    }

    [Fact]
    public void TryResolveCompiledRuleAsFactBag_ReturnsNull_WhenFactoryIsMissing()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        RulesEngineService service = new(new InMemoryStore(), serviceProvider: provider);
        RuleGraphEntry entry = new()
        {
            NodeId = "compiled-adapted",
            RuleCode = "FACTBAG_ADAPTED"
        };

        object? resolved = InvokePrivateInstance(service, "TryResolveCompiledRuleAsFactBag", entry);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteFlowGraphBridgeAsync_Throws_WhenContextTypeMismatches()
    {
        RulesEngineService service = new(new InMemoryStore(), graphParser: new RuleGraphParser(new Muonroi.Core.Abstractions.SeedWorks.MJsonSerializeService()));
        MethodInfo method = typeof(RulesEngineService)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(x => x.Name == "ExecuteFlowGraphBridgeAsync" && x.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(InternalContext));

        Func<Task> act = () =>
        {
            _ = method.Invoke(
                service,
                new object?[]
                {
                    "wf-graph",
                    """{"workflowName":"wf-graph","flowGraph":{"nodes":[],"edges":[]}}""",
                    new { Value = 1 },
                    CancellationToken.None
                });
            return Task.CompletedTask;
        };

        TargetInvocationException ex = (await act.Should().ThrowAsync<TargetInvocationException>()).Which;
        ex.InnerException.Should().BeOfType<InvalidDataException>();
        ex.InnerException!.Message.Should().Contain("Flow-graph context type mismatch");
    }

    [Fact]
    public async Task ExecuteLegacyWorkflowAsync_Throws_WhenRuleEvaluationProducesExceptionMessage()
    {
        RulesEngineService service = new(new InMemoryStore());
        Workflow[] workflows = JsonSerializer.Deserialize<Workflow[]>(
            """
            [
              {
                "WorkflowName": "wf-legacy-exception",
                "Rules": [
                  {
                    "RuleName": "BrokenRule",
                    "RuleExpressionType": "LambdaExpression",
                    "Expression": "input1.missing > 0"
                  }
                ]
              }
            ]
            """)!;
        MethodInfo method = typeof(RulesEngineService)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(x => x.Name == "ExecuteLegacyWorkflowAsync" && x.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(int));

        Func<Task> act = async () => _ = await (Task<FactBag>)method.Invoke(service, ["wf-legacy-exception", workflows, 3])!;

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ExecuteLegacyWorkflowAsync_Throws_WhenActionExecutionFails()
    {
        RulesEngineService service = new(new InMemoryStore());
        Workflow[] workflows = JsonSerializer.Deserialize<Workflow[]>(
            """
            [
              {
                "WorkflowName": "wf-legacy-action-fail",
                "Rules": [
                  {
                    "RuleName": "Explode",
                    "RuleExpressionType": "LambdaExpression",
                    "Expression": "true",
                    "Actions": {
                      "OnSuccess": {
                        "Name": "OutputExpression",
                        "Context": {
                          "expression": "input1.missing.toString()"
                        }
                      }
                    }
                  }
                ]
              }
            ]
            """)!;
        MethodInfo method = typeof(RulesEngineService)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(x => x.Name == "ExecuteLegacyWorkflowAsync" && x.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(int));

        Func<Task> act = async () => _ = await (Task<FactBag>)method.Invoke(service, ["wf-legacy-action-fail", workflows, 3])!;

        await act.Should().ThrowAsync<Exception>();
    }

    private static object InvokePrivateStatic(string typeMarker, string methodName, params object[] args)
    {
        _ = typeMarker;
        Type type = typeof(RulesEngineService);
        MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
        return method.Invoke(null, args)!;
    }

    private static object? InvokePrivateInstance(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        return method.Invoke(instance, args);
    }

    private static object? InvokePrivateTaskInstance(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        object? result = method.Invoke(instance, args);
        if (result is null)
        {
            return null;
        }

        Type taskType = result.GetType();
        return taskType.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)!.GetValue(result);
    }

    private static object InvokePrivateGenericInstance(object instance, string methodName, Type genericArgument, object[] args)
    {
        MethodInfo method = instance.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(x => x.Name == methodName && x.IsGenericMethodDefinition);
        return method.MakeGenericMethod(genericArgument).Invoke(instance, args)!;
    }

    private static T? GetProperty<T>(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return (T?)property.GetValue(instance);
    }

    private static T? GetTupleItem<T>(object instance, string propertyName)
    {
        FieldInfo field = instance.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.Public)!;
        return (T?)field.GetValue(instance);
    }

    public sealed class InternalContext
    {
        public int Value { get; init; }
    }

    private sealed class PrivateCtorRule : IRule<InternalContext>
    {
        private PrivateCtorRule()
        {
        }

        public string Code => "PRIVATE_CTOR_RULE";

        public Task<RuleResult> EvaluateAsync(InternalContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("private", ctx.Value + 1);
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class InMemoryStore : IRuleSetStore
    {
        public Task SaveAsync(string workflowName, string json, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetAsync(string workflowName, int? version = null, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<int>());
        public Task<int?> GetActiveVersionAsync(string workflowName, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public Task SetActiveVersionAsync(string workflowName, int version, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetWorkflowsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class RecordingStore : IRuleSetStore
    {
        public string? LatestJson { get; init; }
        public string? LastGetWorkflowName { get; private set; }
        public int? LastGetVersion { get; private set; }

        public Task SaveAsync(string workflowName, string json, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetAsync(string workflowName, int? version = null, CancellationToken cancellationToken = default)
        {
            LastGetWorkflowName = workflowName;
            LastGetVersion = version;
            return Task.FromResult(LatestJson);
        }

        public Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<int>());
        public Task<int?> GetActiveVersionAsync(string workflowName, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public Task SetActiveVersionAsync(string workflowName, int version, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetWorkflowsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class RecordingRuntimeCache : IRuleSetRuntimeCache
    {
        public string? LastTenantId { get; private set; }
        public string? LastWorkflowName { get; private set; }

        public Task<string?> GetOrCreateAsync(string tenantId, string workflowName, Func<Task<string?>> factory, CancellationToken cancellationToken = default)
        {
            return factory();
        }

        public Task InvalidateAsync(string tenantId, string workflowName, CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            LastWorkflowName = workflowName;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotifier : IRuleSetChangeNotifier
    {
        public List<RuleSetChangeEvent> Events { get; } = [];

        public Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(changeEvent);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler)
        {
            return SubstituteDisposable.Instance;
        }
    }

    private sealed class SubstituteDisposable : IDisposable
    {
        public static readonly SubstituteDisposable Instance = new();
        public void Dispose() { }
    }

    public sealed class AdaptedContext
    {
        public int Value { get; init; }
    }

    private sealed class AdaptedFactBagRule : IRule<AdaptedContext>
    {
        public string Code => "FACTBAG_ADAPTED";

        public Task<RuleResult> EvaluateAsync(AdaptedContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("adapted", ctx.Value * 4);
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class AdaptedContextFactory : IContextFactory<AdaptedContext>
    {
        public AdaptedContext Create(FactBag facts)
        {
            return new AdaptedContext
            {
                Value = facts.Get<int>("Value")
            };
        }
    }
}
