using FluentAssertions;
using Microsoft.Extensions.Options;
using Muonroi.RuleEngine.Runtime.Tracing;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleDebuggerModeServiceTests
{
    [Fact]
    public async Task IsDebugEnabledAsync_WhenKeyExists_ReturnsTrue()
    {
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        IDatabase database = Substitute.For<IDatabase>();
        connection.GetDatabase(default, default!).ReturnsForAnyArgs(database);
        database.StringGetAsync("dbg:tenant-a", Arg.Any<CommandFlags>()).Returns("1");

        RuleDebuggerModeService sut = new(
            connection,
            Options.Create(new RuleTracingOptions
            {
                Database = 3,
                DebuggerKeyPrefix = "dbg"
            }));

        bool enabled = await sut.IsDebugEnabledAsync("tenant-a");

        enabled.Should().BeTrue();
    }

    [Fact]
    public async Task IsDebugEnabledAsync_WhenKeyMissing_ReturnsFalse()
    {
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        IDatabase database = Substitute.For<IDatabase>();
        connection.GetDatabase(default, default!).ReturnsForAnyArgs(database);
        database.StringGetAsync("rule-debugger:enabled:tenant-a", Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        RuleDebuggerModeService sut = new(connection, Options.Create(new RuleTracingOptions()));

        bool enabled = await sut.IsDebugEnabledAsync("tenant-a");

        enabled.Should().BeFalse();
    }

    [Fact]
    public async Task EnableAsync_WithNonPositiveDuration_ResolvesConfiguredDatabase()
    {
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        IDatabase database = Substitute.For<IDatabase>();
        connection.GetDatabase(default, default!).ReturnsForAnyArgs(database);

        RuleDebuggerModeService sut = new(connection, Options.Create(new RuleTracingOptions()));

        await sut.EnableAsync("tenant-a", TimeSpan.Zero);

        connection.Received(1).GetDatabase(0, Arg.Any<object>());
    }

    [Fact]
    public async Task DisableAsync_DeletesDebuggerKey()
    {
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        IDatabase database = Substitute.For<IDatabase>();
        connection.GetDatabase(default, default!).ReturnsForAnyArgs(database);

        RuleDebuggerModeService sut = new(connection, Options.Create(new RuleTracingOptions()));

        await sut.DisableAsync("tenant-a");

        await database.Received(1).KeyDeleteAsync(
            "rule-debugger:enabled:tenant-a",
            CommandFlags.None);
    }
}
