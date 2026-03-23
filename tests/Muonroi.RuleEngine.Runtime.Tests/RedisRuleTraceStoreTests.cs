using FluentAssertions;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleEngine.Core.Tracing;
using Muonroi.RuleEngine.Runtime.Tracing;
using NSubstitute;
using StackExchange.Redis;
using System.Net;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RedisRuleTraceStoreTests
{
    [Fact]
    public async Task SaveAsync_WithNonPositiveTtl_UsesDefaultTtl()
    {
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        IDatabase database = Substitute.For<IDatabase>();
        IMJsonSerializeService json = Substitute.For<IMJsonSerializeService>();
        connection.GetDatabase(2, Arg.Any<object>()).Returns(database);
        json.Serialize(Arg.Any<RuleTraceEntry>()).Returns("{\"trace\":1}");

        RedisRuleTraceStore sut = new(
            connection,
            Options.Create(new RuleTracingOptions
            {
                Database = 2,
                DefaultTtl = TimeSpan.FromMinutes(45),
                TraceKeyPrefix = "trace"
            }),
            json);

        RuleTraceEntry entry = new()
        {
            TenantId = "tenant-a",
            CorrelationId = "corr-1",
            TraceId = "trace-1",
            ExecutedAt = DateTimeOffset.UtcNow
        };

        await sut.SaveAsync(entry, TimeSpan.Zero);

        bool matched = database.ReceivedCalls().Any(call =>
        {
            if (call.GetMethodInfo().Name != nameof(IDatabase.StringSetAsync))
            {
                return false;
            }

            object?[] args = call.GetArguments();
            return args.Length >= 3
                && string.Equals(args[0]?.ToString(), "trace:tenant-a:corr-1:trace-1", StringComparison.Ordinal)
                && string.Equals(args[1]?.ToString(), "{\"trace\":1}", StringComparison.Ordinal);
        });

        matched.Should().BeTrue();
    }

    [Fact]
    public async Task QueryAsync_FiltersInvalidPayloads_AndSortsByExecutedAt()
    {
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        IDatabase database = Substitute.For<IDatabase>();
        IServer server = Substitute.For<IServer>();
        IMJsonSerializeService json = Substitute.For<IMJsonSerializeService>();
        EndPoint endpoint = new IPEndPoint(IPAddress.Loopback, 6379);

        connection.GetDatabase(0, Arg.Any<object>()).Returns(database);
        connection.GetEndPoints(Arg.Any<bool>()).Returns([endpoint]);
        connection.GetServer(endpoint, Arg.Any<object>()).Returns(server);
        server.Keys(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(new RedisKey[] { "k1", "k2", "k3" });
        database.StringGetAsync(Arg.Any<RedisKey[]>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue[] { "payload-1", "payload-2", "payload-3" });

        RuleTraceEntry newer = new()
        {
            TenantId = "tenant-a",
            CorrelationId = "corr-1",
            TraceId = "trace-2",
            ExecutedAt = new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero)
        };
        RuleTraceEntry older = new()
        {
            TenantId = "tenant-a",
            CorrelationId = "corr-1",
            TraceId = "trace-1",
            ExecutedAt = new DateTimeOffset(2026, 3, 21, 9, 0, 0, TimeSpan.Zero)
        };

        json.Deserialize<RuleTraceEntry>("payload-1").Returns(older);
        json.Deserialize<RuleTraceEntry>("payload-2").Returns(newer);
        json.When(x => x.Deserialize<RuleTraceEntry>("payload-3"))
            .Do(_ => throw new InvalidOperationException("corrupt"));

        RedisRuleTraceStore sut = new(connection, Options.Create(new RuleTracingOptions()), json);

        IReadOnlyList<RuleTraceEntry> result = await sut.QueryAsync(
            "tenant-a",
            correlationId: null,
            from: new DateTimeOffset(2026, 3, 21, 8, 30, 0, TimeSpan.Zero));

        result.Should().HaveCount(2);
        result[0].TraceId.Should().Be("trace-2");
        result[1].TraceId.Should().Be("trace-1");
    }
}
