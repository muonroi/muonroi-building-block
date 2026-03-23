namespace Muonroi.Rules.Tests.Rules;

public class InMemoryRuleSetChangeNotifierTests
{
    [Fact]
    public async Task PublishAsync_ShouldInvokeSubscribedHandlers()
    {
        var notifier = new InMemoryRuleSetChangeNotifier();
        RuleSetChangeEvent? received = null;
        notifier.Subscribe(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var expected = new RuleSetChangeEvent("t1", "wf1", "saved", 1, DateTimeOffset.UtcNow);
        await notifier.PublishAsync(expected);

        received.Should().Be(expected);
    }

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_ShouldInvokeAll()
    {
        var notifier = new InMemoryRuleSetChangeNotifier();
        int count = 0;
        notifier.Subscribe(_ => { count++; return Task.CompletedTask; });
        notifier.Subscribe(_ => { count++; return Task.CompletedTask; });

        await notifier.PublishAsync(new RuleSetChangeEvent("t", "w", "saved", null, DateTimeOffset.UtcNow));

        count.Should().Be(2);
    }

    [Fact]
    public async Task Subscribe_Dispose_ShouldUnsubscribe()
    {
        var notifier = new InMemoryRuleSetChangeNotifier();
        int count = 0;
        var sub = notifier.Subscribe(_ => { count++; return Task.CompletedTask; });
        sub.Dispose();

        await notifier.PublishAsync(new RuleSetChangeEvent("t", "w", "saved", null, DateTimeOffset.UtcNow));

        count.Should().Be(0);
    }

    [Fact]
    public async Task Subscribe_DoubleDispose_ShouldNotThrow()
    {
        var notifier = new InMemoryRuleSetChangeNotifier();
        var sub = notifier.Subscribe(_ => Task.CompletedTask);

        sub.Dispose();
        Action act = () => sub.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_ShouldNotThrow()
    {
        var notifier = new InMemoryRuleSetChangeNotifier();

        Func<Task> act = () => notifier.PublishAsync(
            new RuleSetChangeEvent("t", "w", "saved", null, DateTimeOffset.UtcNow));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithCancellation_ShouldThrow()
    {
        var notifier = new InMemoryRuleSetChangeNotifier();
        notifier.Subscribe(_ => Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => notifier.PublishAsync(
            new RuleSetChangeEvent("t", "w", "saved", null, DateTimeOffset.UtcNow), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
