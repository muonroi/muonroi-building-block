using FluentAssertions;
using Muonroi.RuleEngine.Core.Events;
using Muonroi.RuleEngine.Runtime.Events;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests.Events;

public sealed class CloudEventPublishingNotifierTests
{
    private static RuleSetChangeEvent MakeEvent(string tenantId, string workflowName, string changeType, int version = 1)
        => new(tenantId, workflowName, changeType, version, DateTimeOffset.UtcNow);

    // ----------------------------------------------------------------
    // Test 1: PublishAsync delegates to inner AND publishes CloudEvent
    // ----------------------------------------------------------------
    [Fact]
    public async Task PublishAsync_DelegatesToInnerAndPublishesToSink()
    {
        InMemoryRuleSetChangeNotifier inner = new();
        InMemoryEventSink sink = new();
        CloudEventPublishingNotifier sut = new(inner, sink);

        RuleSetChangeEvent? received = null;
        inner.Subscribe(e => { received = e; return Task.CompletedTask; });

        RuleSetChangeEvent evt = MakeEvent("t1", "wf1", RuleSetChangeTypes.Saved);
        await sut.PublishAsync(evt);

        received.Should().NotBeNull("inner should be called");
        sink.Events.Should().HaveCount(1, "one CloudEvent should be published");
    }

    // ----------------------------------------------------------------
    // Test 2: Maps ChangeType to correct CloudEvent type
    // ----------------------------------------------------------------
    [Theory]
    [InlineData(RuleSetChangeTypes.Saved, RuleCloudEventTypes.RuleSetChanged)]
    [InlineData(RuleSetChangeTypes.SubmittedForApproval, RuleCloudEventTypes.RuleSetChanged)]
    [InlineData(RuleSetChangeTypes.Approved, RuleCloudEventTypes.RuleSetChanged)]
    [InlineData(RuleSetChangeTypes.Rejected, RuleCloudEventTypes.RuleSetChanged)]
    [InlineData(RuleSetChangeTypes.CanaryStarted, RuleCloudEventTypes.RuleSetChanged)]
    [InlineData(RuleSetChangeTypes.CanaryPromoted, RuleCloudEventTypes.RuleSetChanged)]
    [InlineData(RuleSetChangeTypes.CanaryRolledBack, RuleCloudEventTypes.RuleSetChanged)]
    [InlineData(RuleSetChangeTypes.Activated, RuleCloudEventTypes.RuleSetActivated)]
    public async Task PublishAsync_MapsChangeTypeToCloudEventType(string changeType, string expectedCloudType)
    {
        InMemoryRuleSetChangeNotifier inner = new();
        InMemoryEventSink sink = new();
        CloudEventPublishingNotifier sut = new(inner, sink);

        await sut.PublishAsync(MakeEvent("t1", "wf1", changeType));

        sink.Events[0].Type.Should().Be(expectedCloudType);
    }

    // ----------------------------------------------------------------
    // Test 3: Subscribe delegates to inner
    // ----------------------------------------------------------------
    [Fact]
    public async Task Subscribe_DelegatesToInner()
    {
        InMemoryRuleSetChangeNotifier inner = new();
        InMemoryEventSink sink = new();
        CloudEventPublishingNotifier sut = new(inner, sink);

        int callCount = 0;
        sut.Subscribe(_ => { callCount++; return Task.CompletedTask; });

        await sut.PublishAsync(MakeEvent("t1", "wf1", RuleSetChangeTypes.Saved));

        callCount.Should().Be(1);
    }

    // ----------------------------------------------------------------
    // Test 4: CloudEvent Subject is "{tenantId}/{workflowName}"
    // ----------------------------------------------------------------
    [Fact]
    public async Task PublishAsync_CloudEventSubjectIsTenantSlashWorkflow()
    {
        InMemoryRuleSetChangeNotifier inner = new();
        InMemoryEventSink sink = new();
        CloudEventPublishingNotifier sut = new(inner, sink);

        await sut.PublishAsync(MakeEvent("tenant-abc", "my-workflow", RuleSetChangeTypes.Saved));

        sink.Events[0].Subject.Should().Be("tenant-abc/my-workflow");
    }

    // ----------------------------------------------------------------
    // Test 5: CloudEvent Source is "/muonroi/rule-engine"
    // ----------------------------------------------------------------
    [Fact]
    public async Task PublishAsync_CloudEventSourceIsRuleEngine()
    {
        InMemoryRuleSetChangeNotifier inner = new();
        InMemoryEventSink sink = new();
        CloudEventPublishingNotifier sut = new(inner, sink);

        await sut.PublishAsync(MakeEvent("t1", "wf1", RuleSetChangeTypes.Activated));

        sink.Events[0].Source.Should().Be("/muonroi/rule-engine");
    }
}
