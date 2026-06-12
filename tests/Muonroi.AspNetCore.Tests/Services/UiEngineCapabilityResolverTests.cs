using Muonroi.Quota.Abstractions;

namespace Muonroi.AspNetCore.Tests.Services;

public class UiEngineCapabilityResolverTests
{
    private sealed class FakeContributor : IUiEngineManifestContributor
    {
        public string ModuleId { get; set; } = string.Empty;
        public string RequiredTier { get; set; } = "Free";
        public int Order { get; set; }
        public Task ContributeAsync(UiEngineManifestContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public void BuildCapabilities_NoContributors_ReturnsEmpty()
    {
        var resolver = new UiEngineCapabilityResolver([]);
        var capabilities = resolver.BuildCapabilities(TenantTier.Free);
        capabilities.Should().BeEmpty();
    }

    [Fact]
    public void BuildCapabilities_SingleContributor_ReturnsRuleEngineAndModule()
    {
        var contributors = new IUiEngineManifestContributor[]
        {
            new FakeContributor { ModuleId = "decision-table", RequiredTier = "Free" }
        };
        var resolver = new UiEngineCapabilityResolver(contributors);

        var capabilities = resolver.BuildCapabilities(TenantTier.Enterprise);

        capabilities.Should().HaveCount(2);
        capabilities[0].CapabilityKey.Should().Be("rule-engine");
        capabilities[1].CapabilityKey.Should().Be("decision-table");
    }

    [Fact]
    public void BuildCapabilities_TenantTierLowerThanRequired_IsEnabledFalse()
    {
        var contributors = new IUiEngineManifestContributor[]
        {
            new FakeContributor { ModuleId = "premium-feature", RequiredTier = "Enterprise" }
        };
        var resolver = new UiEngineCapabilityResolver(contributors);

        var capabilities = resolver.BuildCapabilities(TenantTier.Free);

        capabilities.Should().Contain(c => c.CapabilityKey == "premium-feature" && !c.IsEnabled);
    }

    [Fact]
    public void BuildCapabilities_TenantTierMatchesRequired_IsEnabledTrue()
    {
        var contributors = new IUiEngineManifestContributor[]
        {
            new FakeContributor { ModuleId = "feature-a", RequiredTier = "Professional" }
        };
        var resolver = new UiEngineCapabilityResolver(contributors);

        var capabilities = resolver.BuildCapabilities(TenantTier.Professional);

        capabilities.Should().Contain(c => c.CapabilityKey == "feature-a" && c.IsEnabled);
    }

    [Fact]
    public void BuildCapabilities_BlankModuleId_Skipped()
    {
        var contributors = new IUiEngineManifestContributor[]
        {
            new FakeContributor { ModuleId = "", RequiredTier = "Free" },
            new FakeContributor { ModuleId = "valid-module", RequiredTier = "Free" }
        };
        var resolver = new UiEngineCapabilityResolver(contributors);

        var capabilities = resolver.BuildCapabilities(TenantTier.Free);

        capabilities.Should().HaveCount(2); // rule-engine + valid-module
        capabilities.Should().NotContain(c => string.IsNullOrWhiteSpace(c.CapabilityKey));
    }

    [Fact]
    public void BuildCapabilities_RuleEngineContributor_UsesItsOwnTier()
    {
        var contributors = new IUiEngineManifestContributor[]
        {
            new FakeContributor { ModuleId = "rule-engine", RequiredTier = "Enterprise" },
            new FakeContributor { ModuleId = "feature-a", RequiredTier = "Free" }
        };
        var resolver = new UiEngineCapabilityResolver(contributors);

        var capabilities = resolver.BuildCapabilities(TenantTier.Free);

        var ruleEngine = capabilities.First(c => c.CapabilityKey == "rule-engine");
        ruleEngine.RequiredTier.Should().Be("Enterprise");
        ruleEngine.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void BuildCapabilities_MultipleContributorsSameModule_UsesHighestTier()
    {
        var contributors = new IUiEngineManifestContributor[]
        {
            new FakeContributor { ModuleId = "feature-a", RequiredTier = "Free" },
            new FakeContributor { ModuleId = "feature-a", RequiredTier = "Enterprise" }
        };
        var resolver = new UiEngineCapabilityResolver(contributors);

        var capabilities = resolver.BuildCapabilities(TenantTier.Professional);

        capabilities.Where(c => c.CapabilityKey == "feature-a").Should().HaveCount(1);
        capabilities.First(c => c.CapabilityKey == "feature-a").IsEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Free", TenantTier.Free)]
    [InlineData("Starter", TenantTier.Starter)]
    [InlineData("Professional", TenantTier.Professional)]
    [InlineData("Enterprise", TenantTier.Enterprise)]
    [InlineData("invalid", TenantTier.Free)]
    [InlineData(null, TenantTier.Free)]
    [InlineData("", TenantTier.Free)]
    public void ParseTier_VariousInputs_ReturnsExpected(string? input, TenantTier expected)
    {
        UiEngineCapabilityResolver.ParseTier(input).Should().Be(expected);
    }

    [Fact]
    public void BuildCapabilities_DisplayName_FormattedCorrectly()
    {
        var contributors = new IUiEngineManifestContributor[]
        {
            new FakeContributor { ModuleId = "decision-table", RequiredTier = "Free" }
        };
        var resolver = new UiEngineCapabilityResolver(contributors);

        var capabilities = resolver.BuildCapabilities(TenantTier.Free);

        capabilities.First(c => c.CapabilityKey == "decision-table")
            .DisplayName.Should().Be("Decision Table");
    }
}
