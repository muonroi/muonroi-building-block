using FluentAssertions;
using Muonroi.RuleEngine.Core.Events;
using Muonroi.RuleEngine.Runtime.Events;
using System.Text.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests.Events;

public sealed class RuleExecutionEventPublisherTests
{
    // ----------------------------------------------------------------
    // Test 6: PublishExecutionResultAsync creates muonroi.rule.executed on success
    // ----------------------------------------------------------------
    [Fact]
    public async Task PublishExecutionResultAsync_Success_CreatesExecutedEvent()
    {
        InMemoryEventSink sink = new();
        RuleExecutionEventPublisher sut = new(sink);

        await sut.PublishExecutionResultAsync("t1", "wf1", success: true, ruleCount: 3, elapsedMs: 42.5, errorMessage: null);

        sink.Events.Should().HaveCount(1);
        sink.Events[0].Type.Should().Be(RuleCloudEventTypes.RuleExecuted);
    }

    // ----------------------------------------------------------------
    // Test 7: PublishExecutionResultAsync creates muonroi.rule.failed on failure
    // ----------------------------------------------------------------
    [Fact]
    public async Task PublishExecutionResultAsync_Failure_CreatesFailedEvent()
    {
        InMemoryEventSink sink = new();
        RuleExecutionEventPublisher sut = new(sink);

        await sut.PublishExecutionResultAsync("t1", "wf1", success: false, ruleCount: 2, elapsedMs: 15.0, errorMessage: "rule error");

        sink.Events.Should().HaveCount(1);
        sink.Events[0].Type.Should().Be(RuleCloudEventTypes.RuleFailed);
    }

    // ----------------------------------------------------------------
    // Test 8: Execution metadata included in CloudEvent.Data
    // ----------------------------------------------------------------
    [Fact]
    public async Task PublishExecutionResultAsync_IncludesMetadataInData()
    {
        InMemoryEventSink sink = new();
        RuleExecutionEventPublisher sut = new(sink);

        await sut.PublishExecutionResultAsync("tenant-x", "my-wf", success: true, ruleCount: 5, elapsedMs: 100.0, errorMessage: null);

        CloudEvent evt = sink.Events[0];
        evt.Data.Should().NotBeNull();

        // Data should be serializable to JSON and contain expected fields
        string json = JsonSerializer.Serialize(evt.Data);
        json.Should().Contain("tenant-x");
        json.Should().Contain("my-wf");
        json.Should().Contain("5");
        json.Should().Contain("100");
    }

    // ----------------------------------------------------------------
    // Publisher is fire-and-forget safe: never throws
    // ----------------------------------------------------------------
    [Fact]
    public async Task PublishExecutionResultAsync_SinkThrows_DoesNotPropagate()
    {
        ThrowingEventSink sink = new();
        RuleExecutionEventPublisher sut = new(sink);

        Func<Task> act = () => sut.PublishExecutionResultAsync("t1", "wf1", success: true, ruleCount: 1, elapsedMs: 1.0, errorMessage: null);

        await act.Should().NotThrowAsync("publisher must not propagate sink exceptions");
    }

    private sealed class ThrowingEventSink : IEventSink
    {
        public Task PublishAsync(CloudEvent cloudEvent, CancellationToken ct = default)
            => throw new InvalidOperationException("sink failure");
    }
}
