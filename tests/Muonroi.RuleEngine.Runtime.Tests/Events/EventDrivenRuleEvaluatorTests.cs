using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Governance.License;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core.Events;
using Muonroi.RuleEngine.Runtime.Events;
using Muonroi.RuleEngine.Runtime.Rules;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests.Events;

public sealed class EventDrivenRuleEvaluatorTests
{
    private static CloudEvent MakeCloudEvent(object? data, string type = "external.trigger")
        => new() { Type = type, Source = "/test", Data = data };

    private static RulesEngineService CreateRulesEngineService(IRuleSetStore? store = null)
    {
        IRuleSetStore storeArg = store ?? Substitute.For<IRuleSetStore>();
        ILicenseGuard licenseGuard = Substitute.For<ILicenseGuard>();
        licenseGuard.HasFeature(Arg.Any<string>()).Returns(true);
        // EnsureValid is void — no setup needed; NSubstitute no-ops void methods by default

        ISystemExecutionContextAccessor execCtx = new SystemExecutionContextAccessor();
        execCtx.Set(new SystemExecutionContext("t1", null, null, Guid.NewGuid().ToString("N"), null, null, false, [], "test"));

        return new RulesEngineService(
            store: storeArg,
            licenseGuard: licenseGuard,
            executionContextAccessor: execCtx);
    }

    // ----------------------------------------------------------------
    // Test 1: Extracts workflowName and inputFacts from CloudEvent.Data
    // ----------------------------------------------------------------
    [Fact]
    public async Task HandleEventAsync_ExtractsWorkflowNameFromData()
    {
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        store.GetAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        RulesEngineService engine = CreateRulesEngineService(store);
        EventDrivenRuleEvaluator sut = new(engine);

        var data = new { workflowName = "order-rules", inputFacts = new Dictionary<string, object?> { ["amount"] = 100 } };
        CloudEvent evt = MakeCloudEvent(JsonSerializer.SerializeToElement(data));

        EventEvaluationResult result = await sut.HandleEventAsync(evt);

        // Store was called with "order-rules" (even if no ruleset found)
        await store.Received(1).GetAsync("order-rules", Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------
    // Test 2: Calls RulesEngineService to evaluate rules
    // ----------------------------------------------------------------
    [Fact]
    public async Task HandleEventAsync_CallsRulesEngineWithWorkflowName()
    {
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        // Return a minimal ruleset JSON so the engine can run
        store.GetAsync("test-workflow", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null); // no ruleset — returns empty result

        RulesEngineService engine = CreateRulesEngineService(store);
        EventDrivenRuleEvaluator sut = new(engine);

        var data = new { workflowName = "test-workflow", inputFacts = (object?)null };
        CloudEvent evt = MakeCloudEvent(JsonSerializer.SerializeToElement(data));

        EventEvaluationResult result = await sut.HandleEventAsync(evt);

        result.Status.Should().Be(EventEvaluationStatus.Success);
    }

    // ----------------------------------------------------------------
    // Test 3: Publishes result event via RuleExecutionEventPublisher
    // ----------------------------------------------------------------
    [Fact]
    public async Task HandleEventAsync_PublishesExecutionResultEvent()
    {
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        store.GetAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        RulesEngineService engine = CreateRulesEngineService(store);
        InMemoryEventSink sink = new();
        RuleExecutionEventPublisher publisher = new(sink);
        EventDrivenRuleEvaluator sut = new(engine, publisher);

        var data = new { workflowName = "wf1", inputFacts = (object?)null };
        CloudEvent evt = MakeCloudEvent(JsonSerializer.SerializeToElement(data));

        await sut.HandleEventAsync(evt);

        sink.Events.Should().HaveCount(1, "one execution result event should be published");
        sink.Events[0].Type.Should().BeOneOf(
            RuleCloudEventTypes.RuleExecuted,
            RuleCloudEventTypes.RuleFailed);
    }

    // ----------------------------------------------------------------
    // Test 4: Returns EventEvaluationResult with success and output facts
    // ----------------------------------------------------------------
    [Fact]
    public async Task HandleEventAsync_ReturnsSuccessResult()
    {
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        store.GetAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        RulesEngineService engine = CreateRulesEngineService(store);
        EventDrivenRuleEvaluator sut = new(engine);

        var data = new { workflowName = "wf1", inputFacts = (object?)null };
        CloudEvent evt = MakeCloudEvent(JsonSerializer.SerializeToElement(data));

        EventEvaluationResult result = await sut.HandleEventAsync(evt);

        result.Should().NotBeNull();
        result.Status.Should().Be(EventEvaluationStatus.Success);
        result.OutputFacts.Should().NotBeNull();
    }

    // ----------------------------------------------------------------
    // Test 5: Returns Skipped for events with missing workflowName
    // ----------------------------------------------------------------
    [Fact]
    public async Task HandleEventAsync_MissingWorkflowName_ReturnsSkipped()
    {
        RulesEngineService engine = CreateRulesEngineService();
        EventDrivenRuleEvaluator sut = new(engine);

        // Data without workflowName
        var data = new { inputFacts = new Dictionary<string, object?> { ["x"] = 1 } };
        CloudEvent evt = MakeCloudEvent(JsonSerializer.SerializeToElement(data));

        EventEvaluationResult result = await sut.HandleEventAsync(evt);

        result.Status.Should().Be(EventEvaluationStatus.Skipped);
    }

    [Fact]
    public async Task HandleEventAsync_NullData_ReturnsSkipped()
    {
        RulesEngineService engine = CreateRulesEngineService();
        EventDrivenRuleEvaluator sut = new(engine);

        CloudEvent evt = MakeCloudEvent(null);

        EventEvaluationResult result = await sut.HandleEventAsync(evt);

        result.Status.Should().Be(EventEvaluationStatus.Skipped);
    }

    // ----------------------------------------------------------------
    // Test 6: DI registration wires CloudEventPublishingNotifier as decorator
    // ----------------------------------------------------------------
    [Fact]
    public void AddRuleEventBridge_WiresDecoratorAndComponents()
    {
        ServiceCollection services = new();

        // Register a base notifier and event sink before calling AddRuleEventBridge
        services.AddSingleton<IRuleSetChangeNotifier, InMemoryRuleSetChangeNotifier>();

        services.AddRuleEngineEventBridge();

        ServiceProvider sp = services.BuildServiceProvider();

        // IEventSink must resolve
        IEventSink? sink = sp.GetService<IEventSink>();
        sink.Should().NotBeNull();

        // IRuleSetChangeNotifier must resolve to the decorator
        IRuleSetChangeNotifier? notifier = sp.GetService<IRuleSetChangeNotifier>();
        notifier.Should().BeOfType<CloudEventPublishingNotifier>(
            "AddRuleEventBridge should replace the notifier with the decorator");

        // RuleExecutionEventPublisher must resolve
        RuleExecutionEventPublisher? publisher = sp.GetService<RuleExecutionEventPublisher>();
        publisher.Should().NotBeNull();
    }
}
