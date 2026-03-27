using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class InMemoryRuleSetChangeNotifierTests
{
    [Fact]
    public async Task PublishAsync_WithSubscriber_InvokesHandler()
    {
        InMemoryRuleSetChangeNotifier sut = new();
        RuleSetChangeEvent? received = null;
        sut.Subscribe(e =>
        {
            received = e;
            return Task.CompletedTask;
        });

        RuleSetChangeEvent evt = new("t1", "wf1", RuleSetChangeTypes.Saved, 1, DateTimeOffset.UtcNow);
        await sut.PublishAsync(evt);

        received.Should().NotBeNull();
        received!.TenantId.Should().Be("t1");
        received.WorkflowName.Should().Be("wf1");
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_DoesNotThrow()
    {
        InMemoryRuleSetChangeNotifier sut = new();

        RuleSetChangeEvent evt = new("t1", "wf1", RuleSetChangeTypes.Saved, 1, DateTimeOffset.UtcNow);
        Func<Task> act = () => sut.PublishAsync(evt);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_AllInvoked()
    {
        InMemoryRuleSetChangeNotifier sut = new();
        int count = 0;
        sut.Subscribe(_ => { count++; return Task.CompletedTask; });
        sut.Subscribe(_ => { count++; return Task.CompletedTask; });

        await sut.PublishAsync(new RuleSetChangeEvent("t1", "wf1", "saved", 1, DateTimeOffset.UtcNow));

        count.Should().Be(2);
    }

    [Fact]
    public async Task Subscribe_DisposedSubscription_NoLongerReceivesEvents()
    {
        InMemoryRuleSetChangeNotifier sut = new();
        int count = 0;
        IDisposable sub = sut.Subscribe(_ => { count++; return Task.CompletedTask; });

        sub.Dispose();
        await sut.PublishAsync(new RuleSetChangeEvent("t1", "wf1", "saved", 1, DateTimeOffset.UtcNow));

        count.Should().Be(0);
    }

    [Fact]
    public void Subscribe_DoubleDispose_DoesNotThrow()
    {
        InMemoryRuleSetChangeNotifier sut = new();
        IDisposable sub = sut.Subscribe(_ => Task.CompletedTask);

        Action act = () =>
        {
            sub.Dispose();
            sub.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task PublishAsync_CancellationRequested_Throws()
    {
        InMemoryRuleSetChangeNotifier sut = new();
        sut.Subscribe(_ => Task.CompletedTask);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = () => sut.PublishAsync(
            new RuleSetChangeEvent("t1", "wf1", "saved", 1, DateTimeOffset.UtcNow), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
