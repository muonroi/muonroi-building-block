using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Web.Hubs;
using Muonroi.RuleEngine.Runtime.Web.Services;
using NSubstitute;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleSetHubNotifierTests
{
    [Fact]
    public async Task StartAsync_ShouldSubscribe_AndPushEventsToTenantGroup()
    {
        TestRuleSetChangeNotifier notifier = new();
        IHubContext<RuleSetChangeHub> hubContext = Substitute.For<IHubContext<RuleSetChangeHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy proxy = Substitute.For<IClientProxy>();
        IMLog<RuleSetHubNotifier> logger = Substitute.For<IMLog<RuleSetHubNotifier>>();

        hubContext.Clients.Returns(clients);
        clients.Group("tenant:tenant-a").Returns(proxy);

        RuleSetHubNotifier service = new(notifier, hubContext, logger);
        RuleSetChangeEvent changeEvent = new("tenant-a", "wf-a", RuleSetChangeTypes.Saved, 1, DateTimeOffset.UtcNow);

        await service.StartAsync(default);
        await notifier.PublishAsync(changeEvent);

        await proxy.Received(1).SendCoreAsync(
            "RuleSetChanged",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], changeEvent)),
            Arg.Any<CancellationToken>());
        logger.Received().Info(Arg.Any<string>(), Arg.Any<object?[]>());
    }

    [Fact]
    public async Task StopAsync_ShouldDisposeSubscription_AndPreventFurtherNotifications()
    {
        TestRuleSetChangeNotifier notifier = new();
        IHubContext<RuleSetChangeHub> hubContext = Substitute.For<IHubContext<RuleSetChangeHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy proxy = Substitute.For<IClientProxy>();

        hubContext.Clients.Returns(clients);
        clients.Group("tenant:default").Returns(proxy);

        RuleSetHubNotifier service = new(notifier, hubContext, Substitute.For<IMLog<RuleSetHubNotifier>>());
        await service.StartAsync(default);
        await service.StopAsync(default);
        await notifier.PublishAsync(new RuleSetChangeEvent(string.Empty, "wf-a", RuleSetChangeTypes.Saved, null, DateTimeOffset.UtcNow));

        notifier.DisposeCount.Should().Be(1);
        await proxy.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }

    private sealed class TestRuleSetChangeNotifier : IRuleSetChangeNotifier
    {
        private readonly List<Subscription> _subscriptions = [];

        public int DisposeCount { get; private set; }

        public Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default)
        {
            return Task.WhenAll(_subscriptions
                .Where(subscription => !subscription.IsDisposed)
                .Select(subscription => subscription.Handler(changeEvent)));
        }

        public IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler)
        {
            Subscription subscription = new(handler, this);
            _subscriptions.Add(subscription);
            return subscription;
        }

        private sealed class Subscription(Func<RuleSetChangeEvent, Task> handler, TestRuleSetChangeNotifier owner) : IDisposable
        {
            public Func<RuleSetChangeEvent, Task> Handler { get; } = handler;

            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                owner.DisposeCount++;
            }
        }
    }
}
