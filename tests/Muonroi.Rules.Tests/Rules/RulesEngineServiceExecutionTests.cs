namespace Muonroi.Rules.Tests.Rules;

public class RulesEngineServiceExecutionTests
{
    [Fact]
    public async Task ExecuteAsync_LegacyWorkflow_ShouldReturnFactBag()
    {
        // Create an in-memory FileRuleSetStore to use as the backing store
        string rootPath = Path.Combine(Path.GetTempPath(), $"rules-exec-test-{Guid.NewGuid():N}");
        try
        {
            var store = new FileRuleSetStore(rootPath);

            // Valid MS RulesEngine JSON format with integer enum values
            string json = """
            [
                {
                    "WorkflowName": "TestWorkflow",
                    "Rules": [
                        {
                            "RuleName": "AgeCheck",
                            "RuleExpressionType": 0,
                            "Expression": "input1.value.Age >= 18",
                            "SuccessEvent": "10"
                        }
                    ]
                }
            ]
            """;

            await store.SaveAsync("TestWorkflow", json);

            var service = new RulesEngineService(store);
            var context = new { Age = 25 };
            var bag = await service.ExecuteAsync("TestWorkflow", context);

            bag.Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithRuntimeCache_ShouldUseCache()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"rules-cache-test-{Guid.NewGuid():N}");
        try
        {
            var store = new FileRuleSetStore(rootPath);
            var configs = new RuleStoreConfigs { EnableRuntimeCache = true, RuntimeCacheMinutes = 10 };
            var memCache = new MemoryCache(new MemoryCacheOptions());
            var runtimeCache = new RuleSetRuntimeCache(memCache, configs);

            string json = """
            [
                {
                    "WorkflowName": "CachedWorkflow",
                    "Rules": [
                        {
                            "RuleName": "SimpleRule",
                            "RuleExpressionType": 0,
                            "Expression": "true",
                            "SuccessEvent": "1"
                        }
                    ]
                }
            ]
            """;

            await store.SaveAsync("CachedWorkflow", json);

            var service = new RulesEngineService(store, runtimeCache: runtimeCache);
            var bag1 = await service.ExecuteAsync<object>("CachedWorkflow", new { });
            var bag2 = await service.ExecuteAsync<object>("CachedWorkflow", new { });

            bag1.Should().NotBeNull();
            bag2.Should().NotBeNull();

            memCache.Dispose();
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithNotifier_ShouldNotifyOnSave()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"rules-notify-test-{Guid.NewGuid():N}");
        try
        {
            var store = new FileRuleSetStore(rootPath);
            var notifier = new InMemoryRuleSetChangeNotifier();
            RuleSetChangeEvent? captured = null;
            notifier.Subscribe(evt =>
            {
                captured = evt;
                return Task.CompletedTask;
            });

            var service = new RulesEngineService(store, notifier: notifier);

            string json = """
            [
                {
                    "WorkflowName": "NotifyWorkflow",
                    "Rules": [
                        {
                            "RuleName": "R1",
                            "RuleExpressionType": 0,
                            "Expression": "true"
                        }
                    ]
                }
            ]
            """;

            await service.SaveRuleSetAsync("NotifyWorkflow", json);

            captured.Should().NotBeNull();
            captured!.WorkflowName.Should().Be("NotifyWorkflow");
            captured.ChangeType.Should().Be("saved");
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task SaveRuleSetAsync_SingleObjectFormat_ShouldSucceed()
    {
        var store = Substitute.For<IRuleSetStore>();
        var service = new RulesEngineService(store);

        string json = """
        {
            "WorkflowName": "SingleObj",
            "Rules": [
                {
                    "RuleName": "R1",
                    "RuleExpressionType": 0,
                    "Expression": "true"
                }
            ]
        }
        """;

        await service.SaveRuleSetAsync("SingleObj", json);

        await store.Received(1).SaveAsync("SingleObj", json, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveRuleSetAsync_NoRulesProperty_ShouldSucceed()
    {
        // A workflow without Rules property should pass validation
        var store = Substitute.For<IRuleSetStore>();
        var service = new RulesEngineService(store);

        string json = """{"WorkflowName":"NoRules"}""";

        await service.SaveRuleSetAsync("NoRules", json);

        await store.Received(1).SaveAsync("NoRules", json, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActiveVersionAsync_WithRuntimeCache_ShouldInvalidate()
    {
        var store = Substitute.For<IRuleSetStore>();
        var runtimeCache = Substitute.For<IRuleSetRuntimeCache>();
        var service = new RulesEngineService(store, runtimeCache: runtimeCache);

        await service.SetActiveVersionAsync("test", 2);

        await runtimeCache.Received(1).InvalidateAsync(
            Arg.Any<string>(), "test", Arg.Any<CancellationToken>());
    }
}
