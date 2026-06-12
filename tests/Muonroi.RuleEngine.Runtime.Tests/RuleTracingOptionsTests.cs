using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Tracing;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleTracingOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        RuleTracingOptions options = new();

        options.DefaultTtl.Should().Be(TimeSpan.FromHours(24));
        options.ScanPageSize.Should().Be(200);
        options.MaxQueryResults.Should().Be(1000);
        options.Database.Should().Be(0);
        options.TraceKeyPrefix.Should().Be("rule-trace");
        options.DebuggerKeyPrefix.Should().Be("rule-debugger:enabled");
        options.ModeCacheDuration.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void SectionName_IsRuleTracing()
    {
        RuleTracingOptions.SectionName.Should().Be("RuleTracing");
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        RuleTracingOptions options = new()
        {
            DefaultTtl = TimeSpan.FromMinutes(30),
            ScanPageSize = 500,
            MaxQueryResults = 2000,
            Database = 3,
            TraceKeyPrefix = "custom-trace",
            DebuggerKeyPrefix = "custom-debug",
            ModeCacheDuration = TimeSpan.FromSeconds(60)
        };

        options.DefaultTtl.Should().Be(TimeSpan.FromMinutes(30));
        options.ScanPageSize.Should().Be(500);
        options.MaxQueryResults.Should().Be(2000);
        options.Database.Should().Be(3);
        options.TraceKeyPrefix.Should().Be("custom-trace");
        options.DebuggerKeyPrefix.Should().Be("custom-debug");
        options.ModeCacheDuration.Should().Be(TimeSpan.FromSeconds(60));
    }
}
