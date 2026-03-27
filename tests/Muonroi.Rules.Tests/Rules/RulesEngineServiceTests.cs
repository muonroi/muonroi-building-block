namespace Muonroi.Rules.Tests.Rules;

public class RulesEngineServiceTests
{
    private readonly IRuleSetStore _store = Substitute.For<IRuleSetStore>();

    [Fact]
    public async Task SaveRuleSetAsync_EmptyWorkflowName_ShouldThrow()
    {
        var service = new RulesEngineService(_store);

        Func<Task> act = () => service.SaveRuleSetAsync("", "{}");

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Workflow name*");
    }

    [Fact]
    public async Task SaveRuleSetAsync_EmptyJson_ShouldThrow()
    {
        var service = new RulesEngineService(_store);

        Func<Task> act = () => service.SaveRuleSetAsync("test", "");

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*payload is empty*");
    }

    [Fact]
    public async Task SaveRuleSetAsync_InvalidJson_ShouldThrow()
    {
        var service = new RulesEngineService(_store);

        Func<Task> act = () => service.SaveRuleSetAsync("test", "not json");

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*not valid JSON*");
    }

    [Fact]
    public async Task SaveRuleSetAsync_WorkflowNameMismatch_ShouldThrow()
    {
        var service = new RulesEngineService(_store);
        string json = """[{"WorkflowName":"WrongName","Rules":[{"RuleName":"R1","Expression":"true","RuleExpressionType":"LambdaExpression"}]}]""";

        Func<Task> act = () => service.SaveRuleSetAsync("CorrectName", json);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*WorkflowName mismatch*");
    }

    [Fact]
    public async Task SaveRuleSetAsync_ValidPayload_ShouldCallStore()
    {
        var service = new RulesEngineService(_store);
        string json = """[{"WorkflowName":"test","Rules":[{"RuleName":"R1","Expression":"true","RuleExpressionType":"LambdaExpression"}]}]""";

        await service.SaveRuleSetAsync("test", json);

        await _store.Received(1).SaveAsync("test", json, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveRuleSetAsync_WithNotifier_ShouldPublishEvent()
    {
        var notifier = Substitute.For<IRuleSetChangeNotifier>();
        var service = new RulesEngineService(_store, notifier: notifier);
        string json = """[{"WorkflowName":"test","Rules":[{"RuleName":"R1","Expression":"true","RuleExpressionType":"LambdaExpression"}]}]""";

        await service.SaveRuleSetAsync("test", json);

        await notifier.Received(1).PublishAsync(
            Arg.Is<RuleSetChangeEvent>(e => e.WorkflowName == "test" && e.ChangeType == "saved"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActiveVersionAsync_ShouldCallStore()
    {
        var service = new RulesEngineService(_store);

        await service.SetActiveVersionAsync("test", 3);

        await _store.Received(1).SetActiveVersionAsync("test", 3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActiveVersionAsync_WithNotifier_ShouldPublishActivatedEvent()
    {
        var notifier = Substitute.For<IRuleSetChangeNotifier>();
        var service = new RulesEngineService(_store, notifier: notifier);

        await service.SetActiveVersionAsync("test", 3);

        await notifier.Received(1).PublishAsync(
            Arg.Is<RuleSetChangeEvent>(e => e.ChangeType == "activated" && e.Version == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NullJson_ShouldReturnEmptyFactBag()
    {
        _store.GetAsync("missing", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        var service = new RulesEngineService(_store);

        var bag = await service.ExecuteAsync<object>("missing", new { });

        bag.Should().NotBeNull();
        bag.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRulesPayload_ShouldThrow()
    {
        _store.GetAsync("test", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("""[{"WorkflowName":"test","Rules":[]}]"""));

        var service = new RulesEngineService(_store);

        // Empty Rules array — MS RulesEngine throws RuleValidationException
        Func<Task> act = () => service.ExecuteAsync<object>("test", new { });
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SaveRuleSetAsync_ObjectPayload_ShouldAcceptSingleObject()
    {
        var service = new RulesEngineService(_store);
        string json = """{"WorkflowName":"test","Rules":[{"RuleName":"R1","Expression":"true","RuleExpressionType":"LambdaExpression"}]}""";

        await service.SaveRuleSetAsync("test", json);

        await _store.Received(1).SaveAsync("test", json, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveRuleSetAsync_ScalarPayload_ShouldThrow()
    {
        var service = new RulesEngineService(_store);

        Func<Task> act = () => service.SaveRuleSetAsync("test", "42");

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*must be a JSON object or array*");
    }

    [Fact]
    public async Task SaveRuleSetAsync_EmptyRulesArray_ShouldThrow()
    {
        var service = new RulesEngineService(_store);
        string json = """[{"WorkflowName":"test","Rules":[]}]""";

        Func<Task> act = () => service.SaveRuleSetAsync("test", json);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*non-empty array*");
    }

    [Fact]
    public async Task SaveRuleSetAsync_RulesPropertyIsNotArray_ShouldThrow()
    {
        var service = new RulesEngineService(_store);
        string json = """{"WorkflowName":"test","Rules":{"RuleName":"R1"}}""";

        Func<Task> act = () => service.SaveRuleSetAsync("test", json);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*non-empty array*");
    }

    [Fact]
    public async Task SaveRuleSetAsync_ArrayWithNonObjectWorkflow_ShouldThrow()
    {
        var service = new RulesEngineService(_store);

        Func<Task> act = () => service.SaveRuleSetAsync("test", """["bad-workflow"]""");

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*workflow definition is invalid*");
    }

    [Fact]
    public async Task SaveRuleSetAsync_WithRuntimeCache_ShouldInvalidateCache()
    {
        var runtimeCache = Substitute.For<IRuleSetRuntimeCache>();
        var service = new RulesEngineService(_store, runtimeCache: runtimeCache);
        string json = """[{"WorkflowName":"test","Rules":[{"RuleName":"R1","Expression":"true","RuleExpressionType":"LambdaExpression"}]}]""";

        await service.SaveRuleSetAsync("test", json);

        await runtimeCache.Received(1).InvalidateAsync(
            Arg.Any<string>(), "test", Arg.Any<CancellationToken>());
    }
}
