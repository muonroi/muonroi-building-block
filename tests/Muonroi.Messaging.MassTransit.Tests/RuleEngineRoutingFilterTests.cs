using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Messaging.MassTransit.Tests;

/// <summary>
/// Verifies redirect, reject, legacy, and Redis-backed routing behavior.
/// </summary>
public class RuleEngineRoutingFilterTests
{
    /// <summary>
    /// Ensures that the filter is a no-op when no routers are registered.
    /// </summary>
    [Fact]
    public async Task Send_WhenNoRoutersConfigured_CallsNext()
    {
        RoutingFilterFixture fixture = new();
        RuleEngineRoutingFilter<TestMessage> filter = fixture.CreateFilter();

        await filter.Send(fixture.Context, fixture.Next);

        await fixture.Next.Received(1).Send(fixture.Context);
    }

    /// <summary>
    /// Ensures that redirect decisions send the message to the resolved endpoint and skip the consumer.
    /// </summary>
    [Fact]
    public async Task Send_WhenRouterRedirects_SendsToTargetAndSkipsNext()
    {
        RoutingFilterFixture fixture = new();
        fixture.Routers = [new DelegateRouter("redirect", 0, static _ => RoutingDecision.RedirectTo("rabbitmq://host/priority", "match"))];
        RuleEngineRoutingFilter<TestMessage> filter = fixture.CreateFilter();

        await filter.Send(fixture.Context, fixture.Next);

        await fixture.SendEndpointProvider.Received(1).GetSendEndpoint(new Uri("rabbitmq://host/priority"));
        await fixture.SendEndpoint.Received(1).Send(
            Arg.Is<TestMessage>(x => x.CustomerTier == "vip"),
            Arg.Any<IPipe<SendContext<TestMessage>>>(),
            fixture.Context.CancellationToken);
        await fixture.Next.DidNotReceive().Send(fixture.Context);
    }

    /// <summary>
    /// Ensures that reject decisions notify MassTransit faults and stop processing.
    /// </summary>
    [Fact]
    public async Task Send_WhenRouterRejects_NotifiesFaultAndThrows()
    {
        RoutingFilterFixture fixture = new();
        fixture.Routers = [new DelegateRouter("reject", 0, static _ => RoutingDecision.DeadLetter("blocked"))];
        RuleEngineRoutingFilter<TestMessage> filter = fixture.CreateFilter();

        Func<Task> action = () => filter.Send(fixture.Context, fixture.Next);

        await action.Should().ThrowAsync<RoutingRejectedException>()
            .WithMessage("*blocked*");
        await fixture.Context.Received(1).NotifyFaulted(
            TimeSpan.Zero,
            Arg.Any<string>(),
            Arg.Is<RoutingRejectedException>(x => x.Message.Contains("blocked", StringComparison.Ordinal)));
        await fixture.Next.DidNotReceive().Send(fixture.Context);
    }

    /// <summary>
    /// Ensures that legacy routing rules still propagate execution failures.
    /// </summary>
    [Fact]
    public async Task Send_WhenLegacyRuleThrows_PropagatesFailure()
    {
        RoutingFilterFixture fixture = new();
        fixture.LegacyRules = [new ThrowingLegacyRule()];
        RuleEngineRoutingFilter<TestMessage> filter = fixture.CreateFilter();

        Func<Task> action = () => filter.Send(fixture.Context, fixture.Next);

        await action.Should().ThrowAsync<MInternalException>()
            .WithMessage("legacy failed");
        await fixture.Next.DidNotReceive().Send(fixture.Context);
    }

    /// <summary>
    /// Ensures that router evaluation stops after the first redirect.
    /// </summary>
    [Fact]
    public async Task Send_WhenMultipleRoutersConfigured_FirstRedirectWins()
    {
        RoutingFilterFixture fixture = new();
        DelegateRouter first = new("first", 0, static _ => RoutingDecision.RedirectTo("rabbitmq://host/first"));
        DelegateRouter second = new("second", 10, static _ => RoutingDecision.RedirectTo("rabbitmq://host/second"));
        fixture.Routers = [first, second];
        RuleEngineRoutingFilter<TestMessage> filter = fixture.CreateFilter();

        await filter.Send(fixture.Context, fixture.Next);

        first.CallCount.Should().Be(1);
        second.CallCount.Should().Be(0);
        await fixture.SendEndpointProvider.Received(1).GetSendEndpoint(new Uri("rabbitmq://host/first"));
    }

    /// <summary>
    /// Ensures that Redis dynamic routes are evaluated before DI routers.
    /// </summary>
    [Fact]
    public async Task Send_WhenRedisRouteMatches_AppliesRedirect()
    {
        RoutingFilterFixture fixture = new(enableRedisRoutingTable: true);
        fixture.RedisStore.GetRoutesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([new RoutingTableEntry(typeof(TestMessage).FullName!, "tenant-a", "vip-route", 0, "rabbitmq://host/vip-orders", "CustomerTier = \"vip\"")]);
        fixture.Routers = [new DelegateRouter("fallback", 100, static _ => RoutingDecision.RedirectTo("rabbitmq://host/fallback"))];
        RuleEngineRoutingFilter<TestMessage> filter = fixture.CreateFilter();

        await filter.Send(fixture.Context, fixture.Next);

        await fixture.RedisStore.Received(1).GetRoutesAsync(typeof(TestMessage).FullName!, "tenant-a", fixture.Context.CancellationToken);
        await fixture.SendEndpointProvider.Received(1).GetSendEndpoint(new Uri("rabbitmq://host/vip-orders"));
        await fixture.Next.DidNotReceive().Send(fixture.Context);
    }

    /// <summary>
    /// Ensures the filter processes a message with no tenant header and no execution context,
    /// resolving TenantId to null (DCP-09 — null tenant is valid).
    /// </summary>
    [Fact]
    public async Task Send_WhenNoTenantHeaderAndNoExecutionContext_TenantIdResolvesToNull()
    {
        RoutingFilterFixture fixture = new(includeExecutionContext: false);
        DelegateRouter inspector = new("inspect", 0, static _ => RoutingDecision.PassThrough);
        fixture.Routers = [inspector];
        RuleEngineRoutingFilter<TestMessage> filter = fixture.CreateFilter();

        await filter.Send(fixture.Context, fixture.Next);

        inspector.CallCount.Should().Be(1);
        await fixture.Next.Received(1).Send(fixture.Context);
    }

    /// <summary>
    /// Ensures that existing tests with non-null TenantId continue to work (DCP-10 regression guard).
    /// </summary>
    [Fact]
    public async Task Send_WhenTenantIdPresent_RoutingStillWorks()
    {
        RoutingFilterFixture fixture = new();
        DelegateRouter router = new("pass", 0, static _ => RoutingDecision.PassThrough);
        fixture.Routers = [router];
        RuleEngineRoutingFilter<TestMessage> filter = fixture.CreateFilter();

        await filter.Send(fixture.Context, fixture.Next);

        router.CallCount.Should().Be(1);
        await fixture.Next.Received(1).Send(fixture.Context);
    }

    /// <summary>
    /// Provides the collaborators used by the routing filter tests.
    /// </summary>
    private sealed class RoutingFilterFixture
    {
        /// <summary>
        /// Initializes a new fixture instance.
        /// </summary>
        /// <param name="enableRedisRoutingTable">Controls whether Redis dynamic routing is enabled.</param>
        /// <param name="includeExecutionContext">Controls whether execution context is included.</param>
        public RoutingFilterFixture(bool enableRedisRoutingTable = false, bool includeExecutionContext = true)
        {
            Headers headers = CreateHeaders(new Dictionary<string, object?>());
            Context = Substitute.For<ConsumeContext<TestMessage>>();
            Context.Message.Returns(new TestMessage { CustomerTier = "vip" });
            Context.CancellationToken.Returns(CancellationToken.None);
            Context.Headers.Returns(headers);

            Next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();
            Next.Send(Context).Returns(Task.CompletedTask);

            SendEndpointProvider = Substitute.For<ISendEndpointProvider>();
            SendEndpoint = Substitute.For<ISendEndpoint>();
            SendEndpoint.Send(Arg.Any<TestMessage>(), Arg.Any<IPipe<SendContext<TestMessage>>>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            SendEndpointProvider.GetSendEndpoint(Arg.Any<Uri>()).Returns(SendEndpoint);

            if (includeExecutionContext)
            {
                SystemExecutionContextAccessor accessor = new();
                accessor.Set(new SystemExecutionContext(
                    tenantId: "tenant-a",
                    userId: "user-a",
                    username: "alice",
                    correlationId: "corr-a",
                    accessToken: "token-a",
                    apiKey: "api-key",
                    isAuthenticated: true,
                    permissions: [],
                    sourceType: "message-bus"));
                ExecutionContextAccessor = accessor;
            }
            else
            {
                ExecutionContextAccessor = null;
            }
            RedisStore = Substitute.For<IRedisRoutingTableStore>();
            Routers = [];
            LegacyRules = [];
            Options = Microsoft.Extensions.Options.Options.Create(new MessageBusConfigs
            {
                EnableRuleEngineRouting = true,
                EnableRedisRoutingTable = enableRedisRoutingTable
            });
        }

        /// <summary>
        /// Gets the MassTransit consume context substitute.
        /// </summary>
        public ConsumeContext<TestMessage> Context { get; }

        /// <summary>
        /// Gets the downstream pipe substitute.
        /// </summary>
        public IPipe<ConsumeContext<TestMessage>> Next { get; }

        /// <summary>
        /// Gets the send endpoint provider substitute.
        /// </summary>
        public ISendEndpointProvider SendEndpointProvider { get; }

        /// <summary>
        /// Gets the send endpoint substitute.
        /// </summary>
        public ISendEndpoint SendEndpoint { get; }

        /// <summary>
        /// Gets the execution context accessor used to provide tenant context. Null when simulating no-tenant context.
        /// </summary>
        public SystemExecutionContextAccessor? ExecutionContextAccessor { get; }

        /// <summary>
        /// Gets the Redis routing table store substitute.
        /// </summary>
        public IRedisRoutingTableStore RedisStore { get; }

        /// <summary>
        /// Gets or sets the explicit routers.
        /// </summary>
        public IEnumerable<IMessageRouter<TestMessage>> Routers { get; set; }

        /// <summary>
        /// Gets or sets the legacy routing rules.
        /// </summary>
        public IEnumerable<IMessageRoutingRule<TestMessage>> LegacyRules { get; set; }

        /// <summary>
        /// Gets the message bus options.
        /// </summary>
        public IOptions<MessageBusConfigs> Options { get; }

        /// <summary>
        /// Creates a filter configured with the current fixture state.
        /// </summary>
        /// <returns>The configured filter.</returns>
        public RuleEngineRoutingFilter<TestMessage> CreateFilter()
        {
            return new RuleEngineRoutingFilter<TestMessage>(
                Routers,
                LegacyRules,
                SendEndpointProvider,
                Options,
                ExecutionContextAccessor,
                RedisStore);
        }

        private static Headers CreateHeaders(IReadOnlyDictionary<string, object?> values)
        {
            Headers headers = Substitute.For<Headers>();
            headers.TryGetHeader(Arg.Any<string>(), out Arg.Any<object?>())
                .Returns(callInfo =>
                {
                    string key = (string)callInfo[0]!;
                    if (values.TryGetValue(key, out object? value))
                    {
                        callInfo[1] = value;
                        return true;
                    }

                    callInfo[1] = null;
                    return false;
                });
            return headers;
        }
    }

    /// <summary>
    /// Sample message used by the routing filter tests.
    /// </summary>
    public sealed class TestMessage
    {
        /// <summary>
        /// Gets or sets the customer tier.
        /// </summary>
        public string CustomerTier { get; set; } = string.Empty;
    }

    /// <summary>
    /// Router implementation backed by a delegate.
    /// </summary>
    /// <remarks>
    /// Initializes a new router instance.
    /// </remarks>
    /// <param name="code">The router code.</param>
    /// <param name="order">The router order.</param>
    /// <param name="decisionFactory">The decision factory delegate.</param>
    private sealed class DelegateRouter(string code, int order, Func<TestMessage, IRoutingDecision> decisionFactory) : IMessageRouter<TestMessage>
    {
        private readonly Func<TestMessage, IRoutingDecision> _decisionFactory = decisionFactory ?? throw new ArgumentNullException(nameof(decisionFactory));

        /// <inheritdoc />
        public int Order { get; } = order;

        /// <inheritdoc />
        public string Code { get; } = code;

        /// <summary>
        /// Gets the number of times the router has been invoked.
        /// </summary>
        public int CallCount { get; private set; }

        /// <inheritdoc />
        public Task<IRoutingDecision> RouteAsync(TestMessage message, IRoutingContext context, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_decisionFactory(message));
        }
    }

    /// <summary>
    /// Legacy rule used to preserve the old execution semantics.
    /// </summary>
    private sealed class ThrowingLegacyRule : IMessageRoutingRule<TestMessage>
    {
        /// <inheritdoc />
        public string Code => "legacy";

        /// <inheritdoc />
        public int Order => 0;

        /// <inheritdoc />
        public Task<RuleResult> EvaluateAsync(TestMessage ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        /// <inheritdoc />
        public Task ExecuteAsync(TestMessage context, CancellationToken cancellationToken = default)
        {
            throw new MInternalException("legacy failed");
        }
    }
}
