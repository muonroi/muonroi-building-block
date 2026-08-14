namespace Muonroi.RuleGen.Tests;

public class OptionReaderTests
{
    private static CommandContext CreateContext(Dictionary<string, string> options)
    {
        return new CommandContext
        {
            RawOptions = options,
            Config = new RuleGenConfig(),
            WorkingDirectory = Directory.GetCurrentDirectory()
        };
    }

    [Fact]
    public void GetString_ExistingKey_ReturnsValue()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["source"] = "src/Handlers" });
        OptionReader.GetString(ctx, "source").Should().Be("src/Handlers");
    }

    [Fact]
    public void GetString_MissingKey_ReturnsFallback()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        OptionReader.GetString(ctx, "source", "default").Should().Be("default");
    }

    [Fact]
    public void GetString_EmptyValue_ReturnsFallback()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["source"] = "  " });
        OptionReader.GetString(ctx, "source", "fallback").Should().Be("fallback");
    }

    [Fact]
    public void GetString_MissingKey_NoFallback_ReturnsNull()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        OptionReader.GetString(ctx, "missing").Should().BeNull();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("y", true)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("n", false)]
    public void GetBool_VariousFormats_ParsesCorrectly(string value, bool expected)
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["flag"] = value });
        OptionReader.GetBool(ctx, "flag", false).Should().Be(expected);
    }

    [Fact]
    public void GetBool_MissingKey_ReturnsFallback()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        OptionReader.GetBool(ctx, "flag", true).Should().BeTrue();
        OptionReader.GetBool(ctx, "flag", false).Should().BeFalse();
    }

    [Fact]
    public void GetBool_InvalidValue_ReturnsFallback()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["flag"] = "maybe" });
        OptionReader.GetBool(ctx, "flag", true).Should().BeTrue();
    }

    [Fact]
    public void GetCsvList_CommaSeparated_ReturnsAll()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["exclude"] = "*.test,*.spec,*.bak" });
        IReadOnlyList<string> result = OptionReader.GetCsvList(ctx, "exclude");
        result.Should().HaveCount(3);
        result.Should().Contain("*.test");
        result.Should().Contain("*.spec");
        result.Should().Contain("*.bak");
    }

    [Fact]
    public void GetCsvList_SemicolonSeparated_ReturnsAll()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["items"] = "a;b;c" });
        IReadOnlyList<string> result = OptionReader.GetCsvList(ctx, "items");
        result.Should().HaveCount(3);
    }

    [Fact]
    public void GetCsvList_MissingKey_ReturnsFallback()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        IReadOnlyList<string> result = OptionReader.GetCsvList(ctx, "items", ["default"]);
        result.Should().ContainSingle().Which.Should().Be("default");
    }

    [Fact]
    public void GetCsvList_EmptyValue_ReturnsFallback()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["items"] = "" });
        IReadOnlyList<string> result = OptionReader.GetCsvList(ctx, "items");
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCsvList_TrimsEntries()
    {
        CommandContext ctx = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["items"] = " a , b , c " });
        IReadOnlyList<string> result = OptionReader.GetCsvList(ctx, "items");
        result.Should().Equal("a", "b", "c");
    }
}
