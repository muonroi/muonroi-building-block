namespace Muonroi.RuleEngine.Core.Tests.Events;

/// <summary>
/// Tests for InMemoryEventSink — captures events for testing purposes.
/// </summary>
public sealed class InMemoryEventSinkTests
{
    [Fact]
    public async Task PublishAsync_CapuresEventInEventsCollection()
    {
        // Arrange
        var sink = new InMemoryEventSink();
        var evt = new CloudEvent { Type = RuleCloudEventTypes.RuleSetChanged, Source = "/test" };

        // Act
        await sink.PublishAsync(evt);

        // Assert
        Assert.Single(sink.Events);
        Assert.Equal(evt.Id, sink.Events[0].Id);
    }

    [Fact]
    public async Task PublishAsync_MultipleEvents_MaintainsOrder()
    {
        // Arrange
        var sink = new InMemoryEventSink();
        var evt1 = new CloudEvent { Type = RuleCloudEventTypes.RuleSetChanged, Source = "/test" };
        var evt2 = new CloudEvent { Type = RuleCloudEventTypes.RuleSetActivated, Source = "/test" };
        var evt3 = new CloudEvent { Type = RuleCloudEventTypes.RuleExecuted, Source = "/test" };

        // Act
        await sink.PublishAsync(evt1);
        await sink.PublishAsync(evt2);
        await sink.PublishAsync(evt3);

        // Assert
        Assert.Equal(3, sink.Events.Count);
        Assert.Equal(evt1.Id, sink.Events[0].Id);
        Assert.Equal(evt2.Id, sink.Events[1].Id);
        Assert.Equal(evt3.Id, sink.Events[2].Id);
    }

    [Fact]
    public async Task Clear_EmptiesEventsCollection()
    {
        // Arrange
        var sink = new InMemoryEventSink();
        await sink.PublishAsync(new CloudEvent { Type = "t", Source = "s" });
        await sink.PublishAsync(new CloudEvent { Type = "t", Source = "s" });
        Assert.Equal(2, sink.Events.Count);

        // Act
        sink.Clear();

        // Assert
        Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task PublishAsync_AcceptsCancellationToken()
    {
        // Arrange
        var sink = new InMemoryEventSink();
        var evt = new CloudEvent { Type = "t", Source = "s" };
        using var cts = new CancellationTokenSource();

        // Act — should not throw even with a cancellation token
        await sink.PublishAsync(evt, cts.Token);

        // Assert
        Assert.Single(sink.Events);
    }

    [Fact]
    public async Task Events_ReturnsSnapshot_NotLiveReference()
    {
        // Arrange
        var sink = new InMemoryEventSink();

        // Act — snapshot before any additions
        var snapshotBefore = sink.Events;

        // Add after snapshot
        await sink.PublishAsync(new CloudEvent { Type = "t", Source = "s" });

        // Assert — snapshot is immutable, new get reflects latest state
        Assert.Empty(snapshotBefore); // snapshot was empty before publish
        Assert.Single(sink.Events);  // new get includes the published event
    }

    [Fact]
    public async Task IEventSink_PublishAsync_IsAwaitable()
    {
        // Arrange — test via the interface (ensures contract is correct)
        IEventSink sink = new InMemoryEventSink();
        var evt = new CloudEvent { Type = "t", Source = "s" };

        // Act
        await sink.PublishAsync(evt, CancellationToken.None);

        // Assert
        Assert.Single(((InMemoryEventSink)sink).Events);
    }
}
