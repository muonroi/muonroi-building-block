namespace Muonroi.Rules.Tests.Rules;

public class RuleStoreConfigsTests
{
    [Fact]
    public void SectionName_ShouldBeRuleStore()
    {
        RuleStoreConfigs.SectionName.Should().Be("RuleStore");
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new RuleStoreConfigs();

        config.RootPath.Should().BeNull();
        config.UseContentRoot.Should().BeTrue();
        config.MaxRuleSetSizeBytes.Should().Be(1_048_576);
        config.RequireSignature.Should().BeFalse();
        config.AllowedPathSegmentPattern.Should().Be("^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$");
        config.EnableRuntimeCache.Should().BeTrue();
        config.RuntimeCacheMinutes.Should().Be(10);
        config.RuleChangeChannel.Should().Be("muonroi:ruleset:changed");
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var config = new RuleStoreConfigs
        {
            RootPath = "/my/path",
            UseContentRoot = false,
            MaxRuleSetSizeBytes = 500,
            RequireSignature = true,
            AllowedPathSegmentPattern = "^[a-z]+$",
            EnableRuntimeCache = false,
            RuntimeCacheMinutes = 30,
            RuleChangeChannel = "custom:channel"
        };

        config.RootPath.Should().Be("/my/path");
        config.UseContentRoot.Should().BeFalse();
        config.MaxRuleSetSizeBytes.Should().Be(500);
        config.RequireSignature.Should().BeTrue();
        config.AllowedPathSegmentPattern.Should().Be("^[a-z]+$");
        config.EnableRuntimeCache.Should().BeFalse();
        config.RuntimeCacheMinutes.Should().Be(30);
        config.RuleChangeChannel.Should().Be("custom:channel");
    }
}
