namespace Muonroi.Caching.Redis.Tests;

public sealed class RedisRuleSetChangeNotifierTests
{
    [Fact]
    public async Task PublishAsync_SerializesAndPublishesToConfiguredChannel()
    {
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        ISubscriber subscriber = Substitute.For<ISubscriber>();
        IMJsonSerializeService json = Substitute.For<IMJsonSerializeService>();
        connection.GetSubscriber().Returns(subscriber);
        RuleSetChangeEvent change = new("tenant-01", "wf", "saved", 2, DateTimeOffset.UtcNow);
        json.Serialize(change).Returns("{\"workflow\":\"wf\"}");

        RedisRuleSetChangeNotifier notifier = new(connection, "rules:changed", json);

        await notifier.PublishAsync(change);

        json.Received(1).Serialize(change);
        await subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(x => x.ToString() == "rules:changed"),
            Arg.Is<RedisValue>(x => x.ToString() == "{\"workflow\":\"wf\"}"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Subscribe_HandlesMessages_And_UnsubscribeStopsFurtherDelivery()
    {
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        ISubscriber subscriber = Substitute.For<ISubscriber>();
        IMJsonSerializeService json = Substitute.For<IMJsonSerializeService>();
        connection.GetSubscriber().Returns(subscriber);

        Action<RedisChannel, RedisValue>? callback = null;
        subscriber
            .When(x => x.Subscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>()))
            .Do(callInfo => callback = callInfo.Arg<Action<RedisChannel, RedisValue>>());

        RuleSetChangeEvent change = new("tenant-01", "wf", "saved", 2, DateTimeOffset.UtcNow);
        json.Deserialize<RuleSetChangeEvent>("{\"workflow\":\"wf\"}").Returns(change);

        RedisRuleSetChangeNotifier notifier = new(connection, "rules:changed", json);
        RuleSetChangeEvent? received = null;
        IDisposable subscription = notifier.Subscribe(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        callback.Should().NotBeNull();
        callback!(RedisChannel.Literal("rules:changed"), "{\"workflow\":\"wf\"}");
        await Task.Delay(50);

        received.Should().Be(change);

        subscription.Dispose();
        received = null;
        callback!(RedisChannel.Literal("rules:changed"), "{\"workflow\":\"wf\"}");
        await Task.Delay(50);

        received.Should().BeNull();
    }

    [Fact]
    public async Task Subscribe_InvalidPayload_DoesNotInvokeHandlers_AndDisposeUnsubscribes()
    {
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        ISubscriber subscriber = Substitute.For<ISubscriber>();
        IMJsonSerializeService json = Substitute.For<IMJsonSerializeService>();
        connection.GetSubscriber().Returns(subscriber);

        Action<RedisChannel, RedisValue>? callback = null;
        subscriber
            .When(x => x.Subscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>()))
            .Do(callInfo => callback = callInfo.Arg<Action<RedisChannel, RedisValue>>());

        json.When(x => x.Deserialize<RuleSetChangeEvent>(Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("bad payload"));

        RedisRuleSetChangeNotifier notifier = new(connection, "", json);
        bool invoked = false;
        notifier.Subscribe(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        callback.Should().NotBeNull();
        callback!(RedisChannel.Literal("muonroi:ruleset:changed"), "bad-json");
        await Task.Delay(50);

        invoked.Should().BeFalse();

        notifier.Dispose();
        subscriber.Received(1).Unsubscribe(
            Arg.Is<RedisChannel>(x => x.ToString() == "muonroi:ruleset:changed"),
            Arg.Any<Action<RedisChannel, RedisValue>>(),
            Arg.Any<CommandFlags>());
    }
}
