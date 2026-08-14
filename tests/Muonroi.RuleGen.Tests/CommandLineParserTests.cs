namespace Muonroi.RuleGen.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void Parse_EmptyArgs_ReturnsEmptyDictionary()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleKeyValuePair_ReturnsParsed()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse(["--source", "src/Handlers"]);
        result.Should().ContainKey("source").WhoseValue.Should().Be("src/Handlers");
    }

    [Fact]
    public void Parse_MultipleKeyValuePairs_ReturnsAll()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse([
            "--source", "src/Handlers",
            "--output", "Generated/Rules",
            "--namespace", "MyApp.Rules"
        ]);

        result.Should().HaveCount(3);
        result["source"].Should().Be("src/Handlers");
        result["output"].Should().Be("Generated/Rules");
        result["namespace"].Should().Be("MyApp.Rules");
    }

    [Fact]
    public void Parse_FlagWithoutValue_DefaultsToTrue()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse(["--verbose"]);
        result.Should().ContainKey("verbose").WhoseValue.Should().Be("true");
    }

    [Fact]
    public void Parse_FlagFollowedByAnotherFlag_FlagIsTrue()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse(["--verbose", "--debug"]);
        result["verbose"].Should().Be("true");
        result["debug"].Should().Be("true");
    }

    [Fact]
    public void Parse_TokensWithoutDashDash_AreIgnored()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse(["extract", "--source", "src"]);
        result.Should().HaveCount(1);
        result.Should().ContainKey("source");
    }

    [Fact]
    public void Parse_CaseInsensitiveKeys()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse(["--Source", "test"]);
        result.Should().ContainKey("Source");
        // Case insensitive comparer means "source" also works
        result.TryGetValue("source", out string? val).Should().BeTrue();
        val.Should().Be("test");
    }

    [Fact]
    public void Parse_EmptyKeyAfterDashDash_IsSkipped()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse(["--", "value"]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_DuplicateKey_LastValueWins()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse(["--key", "first", "--key", "second"]);
        result["key"].Should().Be("second");
    }

    [Fact]
    public void Parse_LastArgIsFlag_DefaultsToTrue()
    {
        IReadOnlyDictionary<string, string> result = CommandLineParser.Parse(["--source", "src", "--validate"]);
        result["validate"].Should().Be("true");
    }
}
