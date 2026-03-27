using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleStoreConfigsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        RuleStoreConfigs configs = new();

        configs.RootPath.Should().BeNull();
        configs.UseContentRoot.Should().BeTrue();
        configs.MaxRuleSetSizeBytes.Should().Be(1_048_576);
        configs.RequireSignature.Should().BeFalse();
        configs.AllowedPathSegmentPattern.Should().Be("^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$");
        configs.EnableRuntimeCache.Should().BeTrue();
        configs.RuntimeCacheMinutes.Should().Be(10);
        configs.RuleChangeChannel.Should().Be("muonroi:ruleset:changed");
        configs.RequireApproval.Should().BeFalse();
        configs.NotifyOnStateChange.Should().BeTrue();
    }

    [Fact]
    public void SectionName_IsRuleStore()
    {
        RuleStoreConfigs.SectionName.Should().Be("RuleStore");
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        RuleStoreConfigs configs = new()
        {
            RootPath = "/custom/path",
            UseContentRoot = false,
            MaxRuleSetSizeBytes = 500_000,
            RequireSignature = true,
            EnableRuntimeCache = false,
            RuntimeCacheMinutes = 30,
            RequireApproval = true,
            NotifyOnStateChange = false
        };

        configs.RootPath.Should().Be("/custom/path");
        configs.UseContentRoot.Should().BeFalse();
        configs.MaxRuleSetSizeBytes.Should().Be(500_000);
        configs.RequireSignature.Should().BeTrue();
        configs.EnableRuntimeCache.Should().BeFalse();
        configs.RuntimeCacheMinutes.Should().Be(30);
        configs.RequireApproval.Should().BeTrue();
        configs.NotifyOnStateChange.Should().BeFalse();
    }
}
