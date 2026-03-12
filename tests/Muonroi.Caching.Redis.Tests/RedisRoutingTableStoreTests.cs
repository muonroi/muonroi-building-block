namespace Muonroi.Caching.Redis.Tests;

/// <summary>
/// Verifies Redis-backed routing table caching and invalidation behavior.
/// </summary>
public class RedisRoutingTableStoreTests
{
    /// <summary>
    /// Ensures that the first lookup loads active routes from Redis.
    /// </summary>
    [Fact]
    public async Task GetRoutesAsync_OnCacheMiss_LoadsEntriesFromRedis()
    {
        RedisRoutingStoreFixture fixture = new();
        RoutingTableEntry primary = fixture.AddEntry("payload-primary", "primary", 10);

        IReadOnlyList<RoutingTableEntry> result = await fixture.Store.GetRoutesAsync("OrderCreated", "tenant-a");

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(primary);
        await fixture.Database.Received(1).HashValuesAsync("mrt:tenant-a:OrderCreated", Arg.Any<CommandFlags>());
    }

    /// <summary>
    /// Ensures that a cached lookup does not hit Redis again within the TTL window.
    /// </summary>
    [Fact]
    public async Task GetRoutesAsync_OnCacheHit_SkipsSecondRedisCall()
    {
        RedisRoutingStoreFixture fixture = new();
        fixture.AddEntry("payload-primary", "primary", 10);

        IReadOnlyList<RoutingTableEntry> first = await fixture.Store.GetRoutesAsync("OrderCreated", "tenant-a");
        IReadOnlyList<RoutingTableEntry> second = await fixture.Store.GetRoutesAsync("OrderCreated", "tenant-a");

        first.Should().HaveCount(1);
        second.Should().HaveCount(1);
        await fixture.Database.Received(1).HashValuesAsync("mrt:tenant-a:OrderCreated", Arg.Any<CommandFlags>());
    }

    /// <summary>
    /// Ensures that upsert writes the hash field and publishes an invalidation event.
    /// </summary>
    [Fact]
    public async Task UpsertRouteAsync_WritesHashField_AndPublishesInvalidation()
    {
        RedisRoutingStoreFixture fixture = new();
        RoutingTableEntry entry = new("OrderCreated", "tenant-a", "priority", 5, "rabbitmq://host/priority", "total > 10");
        fixture.Json.Serialize(Arg.Is<RoutingTableEntry>(x => x.RuleCode == "priority")).Returns("payload-priority");

        await fixture.Store.UpsertRouteAsync(entry);

        await fixture.Database.Received(1).HashSetAsync(
            "mrt:tenant-a:OrderCreated",
            "priority",
            "payload-priority",
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
        await fixture.Subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(x => x.ToString() == "routing-table-changed:tenant-a:OrderCreated"),
            "tenant-a|OrderCreated",
            Arg.Any<CommandFlags>());
    }

    /// <summary>
    /// Ensures that pub/sub invalidation clears the local cache and forces a reload.
    /// </summary>
    [Fact]
    public async Task PubSub_Invalidation_EvictsLocalCache()
    {
        RedisRoutingStoreFixture fixture = new();
        fixture.AddEntry("payload-primary", "primary", 10);

        IReadOnlyList<RoutingTableEntry> initial = await fixture.Store.GetRoutesAsync("OrderCreated", "tenant-a");
        fixture.Values = [];

        fixture.PublishInvalidation("tenant-a|OrderCreated");

        IReadOnlyList<RoutingTableEntry> refreshed = await fixture.Store.GetRoutesAsync("OrderCreated", "tenant-a");

        initial.Should().HaveCount(1);
        refreshed.Should().BeEmpty();
        await fixture.Database.Received(2).HashValuesAsync("mrt:tenant-a:OrderCreated", Arg.Any<CommandFlags>());
    }

    /// <summary>
    /// Ensures that missing Redis entries return an empty list rather than null.
    /// </summary>
    [Fact]
    public async Task GetRoutesAsync_WhenRedisHasNoEntries_ReturnsEmptyList()
    {
        RedisRoutingStoreFixture fixture = new();

        IReadOnlyList<RoutingTableEntry> result = await fixture.Store.GetRoutesAsync("OrderCreated", "tenant-a");

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Captures the Redis collaborators used by the routing table store.
    /// </summary>
    private sealed class RedisRoutingStoreFixture
    {
        private Action<RedisChannel, RedisValue>? _subscriptionHandler;

        /// <summary>
        /// Initializes a new fixture instance.
        /// </summary>
        public RedisRoutingStoreFixture()
        {
            ConnectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
            Database = Substitute.For<IDatabase>();
            Subscriber = Substitute.For<ISubscriber>();
            Json = Substitute.For<IMJsonSerializeService>();
            DateTimeService = Substitute.For<IMDateTimeService>();
            DateTimeService.UtcNow().Returns(new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc));
            Values = [];

            Database.HashValuesAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
                .Returns(_ => Values.ToArray());
            Subscriber.When(x => x.Subscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>()))
                .Do(callInfo => _subscriptionHandler = callInfo.Arg<Action<RedisChannel, RedisValue>>());

            ConnectionMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(Database);
            ConnectionMultiplexer.GetSubscriber(Arg.Any<object?>()).Returns(Subscriber);

            Store = new RedisRoutingTableStore(
                ConnectionMultiplexer,
                Json,
                DateTimeService,
                Options.Create(new RedisRoutingTableOptions()));
        }

        /// <summary>
        /// Gets the Redis connection substitute.
        /// </summary>
        public IConnectionMultiplexer ConnectionMultiplexer { get; }

        /// <summary>
        /// Gets the Redis database substitute.
        /// </summary>
        public IDatabase Database { get; }

        /// <summary>
        /// Gets the Redis subscriber substitute.
        /// </summary>
        public ISubscriber Subscriber { get; }

        /// <summary>
        /// Gets the JSON serializer substitute.
        /// </summary>
        public IMJsonSerializeService Json { get; }

        /// <summary>
        /// Gets the time service substitute.
        /// </summary>
        public IMDateTimeService DateTimeService { get; }

        /// <summary>
        /// Gets the store under test.
        /// </summary>
        public RedisRoutingTableStore Store { get; }

        /// <summary>
        /// Gets or sets the raw Redis hash values returned by the database.
        /// </summary>
        public List<RedisValue> Values { get; set; }

        /// <summary>
        /// Adds a serialized routing entry to the Redis hash result set.
        /// </summary>
        /// <param name="payload">The payload returned by Redis.</param>
        /// <param name="ruleCode">The rule code.</param>
        /// <param name="order">The route order.</param>
        /// <returns>The registered routing entry.</returns>
        public RoutingTableEntry AddEntry(string payload, string ruleCode, int order)
        {
            RoutingTableEntry entry = new("OrderCreated", "tenant-a", ruleCode, order, $"rabbitmq://host/{ruleCode}", "total > 0");
            Json.Deserialize<RoutingTableEntry>(payload).Returns(entry);
            Values.Add(payload);
            return entry;
        }

        /// <summary>
        /// Pushes a pub/sub invalidation payload into the store subscription.
        /// </summary>
        /// <param name="payload">The invalidation cache key payload.</param>
        public void PublishInvalidation(string payload)
        {
            _subscriptionHandler.Should().NotBeNull();
            _subscriptionHandler!(RedisChannel.Pattern("routing-table-changed:*"), payload);
        }
    }
}
