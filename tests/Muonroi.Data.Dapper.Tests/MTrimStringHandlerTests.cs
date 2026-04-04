using FluentAssertions;
using Muonroi.Data.Dapper.Dapper.Handlers;
using Xunit;

namespace Muonroi.Data.Dapper.Tests;

public class MTrimStringHandlerTests
{
    private readonly MTrimStringHandler _handler = new();

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("hello", "hello")]
    [InlineData("  ", "")]
    [InlineData("test ", "test")]
    [InlineData(" test", "test")]
    public void Parse_Should_Trim_Whitespace(string input, string expected)
    {
        string? result = _handler.Parse(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Parse_Integer_Should_Return_ToString()
    {
        string? result = _handler.Parse(42);

        result.Should().Be("42");
    }

    [Fact]
    public void SetValue_Should_Throw_NotImplementedException()
    {
        Action act = () => _handler.SetValue(null!, "value");

        act.Should().Throw<NotImplementedException>();
    }
}

public class MDapperCommandBuildTests
{
    [Fact]
    public void Build_Should_Create_CommandDefinition()
    {
        MDapperCommand cmd = new()
        {
            CommandText = "SELECT 1",
            Parameters = new { Id = 1 },
            CommandType = System.Data.CommandType.Text
        };

        CommandDefinition definition = cmd.Build(CancellationToken.None);

        definition.CommandText.Should().Be("SELECT 1");
        definition.CommandType.Should().Be(System.Data.CommandType.Text);
    }

    [Fact]
    public void Default_CommandFlag_Should_Be_Buffered()
    {
        MDapperCommand cmd = new();

        cmd.CommandFlag.Should().Be(global::Dapper.CommandFlags.Buffered);
    }

    [Fact]
    public void Default_Properties_Should_Be_Empty_Or_Null()
    {
        MDapperCommand cmd = new();

        cmd.CommandText.Should().BeEmpty();
        cmd.Parameters.Should().BeNull();
        cmd.Transaction.Should().BeNull();
        cmd.CommandType.Should().BeNull();
    }
}
