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
        IMEcosystemRegistry registry = Substitute.For<IMEcosystemRegistry>();
        registry.Has(MCapability.MultiTenant).Returns(true);

        hubContext.Clients.Returns(clients);
        clients.Group("tenant:tenant-a").Returns(proxy);

        // Pass registry so MultiTenant routing is active
        RuleSetHubNotifier service = new(notifier, hubContext, logger, registry);
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
        clients.All.Returns(proxy);

        // null registry -> falls back to Clients.All
        RuleSetHubNotifier service = new(notifier, hubContext, Substitute.For<IMLog<RuleSetHubNotifier>>());
        await service.StartAsync(default);
        await service.StopAsync(default);
        await notifier.PublishAsync(new RuleSetChangeEvent(string.Empty, "wf-a", RuleSetChangeTypes.Saved, null, DateTimeOffset.UtcNow));

        notifier.DisposeCount.Should().Be(1);
        await proxy.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }

    [Fact]
    public async Task NotifyClientsAsync_WithMultiTenantActive_RoutesToTenantGroupAndAllTenantsGroup()
    {
        TestRuleSetChangeNotifier notifier = new();
        IHubContext<RuleSetChangeHub> hubContext = Substitute.For<IHubContext<RuleSetChangeHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy tenantProxy = Substitute.For<IClientProxy>();
        IClientProxy allTenantsProxy = Substitute.For<IClientProxy>();
        IClientProxy allProxy = Substitute.For<IClientProxy>();
        IMLog<RuleSetHubNotifier> logger = Substitute.For<IMLog<RuleSetHubNotifier>>();
        IMEcosystemRegistry registry = Substitute.For<IMEcosystemRegistry>();
        registry.Has(MCapability.MultiTenant).Returns(true);

        hubContext.Clients.Returns(clients);
        clients.Group("tenant:tenant-a").Returns(tenantProxy);
        clients.Group(RuleSetChangeHub.AllTenantsGroup).Returns(allTenantsProxy);
        clients.All.Returns(allProxy);

        RuleSetHubNotifier service = new(notifier, hubContext, logger, registry);
        RuleSetChangeEvent changeEvent = new("tenant-a", "wf-a", RuleSetChangeTypes.Saved, 1, DateTimeOffset.UtcNow);

        await service.StartAsync(default);
        await notifier.PublishAsync(changeEvent);

        // Should send to tenant-specific group
        await tenantProxy.Received(1).SendCoreAsync(
            "RuleSetChanged",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], changeEvent)),
            Arg.Any<CancellationToken>());

        // Should send to all-tenants group
        await allTenantsProxy.Received(1).SendCoreAsync(
            "RuleSetChanged",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], changeEvent)),
            Arg.Any<CancellationToken>());

        // Should NOT send to Clients.All
        await allProxy.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }

    [Fact]
    public async Task NotifyClientsAsync_WithMultiTenantInactive_FallsBackToClientsAll()
    {
        TestRuleSetChangeNotifier notifier = new();
        IHubContext<RuleSetChangeHub> hubContext = Substitute.For<IHubContext<RuleSetChangeHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy allProxy = Substitute.For<IClientProxy>();
        IClientProxy groupProxy = Substitute.For<IClientProxy>();
        IMLog<RuleSetHubNotifier> logger = Substitute.For<IMLog<RuleSetHubNotifier>>();
        IMEcosystemRegistry registry = Substitute.For<IMEcosystemRegistry>();
        registry.Has(MCapability.MultiTenant).Returns(false);

        hubContext.Clients.Returns(clients);
        clients.All.Returns(allProxy);
        clients.Group(Arg.Any<string>()).Returns(groupProxy);

        RuleSetHubNotifier service = new(notifier, hubContext, logger, registry);
        RuleSetChangeEvent changeEvent = new("tenant-a", "wf-a", RuleSetChangeTypes.Saved, 1, DateTimeOffset.UtcNow);

        await service.StartAsync(default);
        await notifier.PublishAsync(changeEvent);

        // Should send to Clients.All
        await allProxy.Received(1).SendCoreAsync(
            "RuleSetChanged",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], changeEvent)),
            Arg.Any<CancellationToken>());

        // Should NOT send to any group
        await groupProxy.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }

    [Fact]
    public async Task NotifyClientsAsync_WithMultiTenantActive_ButNoTenantId_FallsBackToClientsAll()
    {
        TestRuleSetChangeNotifier notifier = new();
        IHubContext<RuleSetChangeHub> hubContext = Substitute.For<IHubContext<RuleSetChangeHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy allProxy = Substitute.For<IClientProxy>();
        IClientProxy groupProxy = Substitute.For<IClientProxy>();
        IMLog<RuleSetHubNotifier> logger = Substitute.For<IMLog<RuleSetHubNotifier>>();
        IMEcosystemRegistry registry = Substitute.For<IMEcosystemRegistry>();
        registry.Has(MCapability.MultiTenant).Returns(true);

        hubContext.Clients.Returns(clients);
        clients.All.Returns(allProxy);
        clients.Group(Arg.Any<string>()).Returns(groupProxy);

        RuleSetHubNotifier service = new(notifier, hubContext, logger, registry);
        // Empty TenantId — falls back to Clients.All even though MultiTenant is active
        RuleSetChangeEvent changeEvent = new("", "wf-a", RuleSetChangeTypes.Saved, 1, DateTimeOffset.UtcNow);

        await service.StartAsync(default);
        await notifier.PublishAsync(changeEvent);

        await allProxy.Received(1).SendCoreAsync(
            "RuleSetChanged",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], changeEvent)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyClientsAsync_WithNullRegistry_FallsBackToClientsAll()
    {
        TestRuleSetChangeNotifier notifier = new();
        IHubContext<RuleSetChangeHub> hubContext = Substitute.For<IHubContext<RuleSetChangeHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy allProxy = Substitute.For<IClientProxy>();
        IClientProxy groupProxy = Substitute.For<IClientProxy>();
        IMLog<RuleSetHubNotifier> logger = Substitute.For<IMLog<RuleSetHubNotifier>>();

        hubContext.Clients.Returns(clients);
        clients.All.Returns(allProxy);
        clients.Group(Arg.Any<string>()).Returns(groupProxy);

        // Null registry — no capability check possible, falls back to Clients.All
        RuleSetHubNotifier service = new(notifier, hubContext, logger, ecosystemRegistry: null);
        RuleSetChangeEvent changeEvent = new("tenant-a", "wf-a", RuleSetChangeTypes.Saved, 1, DateTimeOffset.UtcNow);

        await service.StartAsync(default);
        await notifier.PublishAsync(changeEvent);

        await allProxy.Received(1).SendCoreAsync(
            "RuleSetChanged",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], changeEvent)),
            Arg.Any<CancellationToken>());

        // Should NOT send to any group
        await groupProxy.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
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
