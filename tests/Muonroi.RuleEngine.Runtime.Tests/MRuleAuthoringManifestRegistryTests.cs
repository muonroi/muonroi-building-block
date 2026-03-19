using FluentAssertions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Abstractions.Authoring;
using Muonroi.RuleEngine.Runtime.Rules;
using System.Reflection;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Covers reflection-based authoring manifest discovery.
/// </summary>
public sealed class MRuleAuthoringManifestRegistryTests
{
    /// <summary>
    /// Ensures class-level catalog metadata is carried into the discovered manifest.
    /// </summary>
    [Fact]
    public void GetManifest_ShouldPopulateCatalogMetadataFromRuleAttribute()
    {
        MRuleAuthoringManifestRegistry registry = new(assemblies: [typeof(CatalogVisibleRule).Assembly]);

        MRuleAuthoringManifest manifest = registry.GetManifest(typeof(CatalogVisibleRule).Assembly)!;
        MRuleAuthoringEntry entry = manifest.Rules.Single(rule => rule.Code == "CATALOG_VISIBLE");

        entry.DisplayName.Should().Be("Visible Rule");
        entry.Category.Should().Be("Shipping");
        entry.Icon.Should().Be("pi-ship");
        entry.Tags.Should().Contain(new[] { "liner", "validation" });
        entry.Description.Should().Be("Checks liner metadata.");
        entry.IsPaletteVisible.Should().BeTrue();
    }

    /// <summary>
    /// Ensures missing metadata falls back to safe defaults.
    /// </summary>
    [Fact]
    public void GetManifest_ShouldUseDefaultsWhenCatalogAttributeIsMissing()
    {
        MRuleAuthoringManifestRegistry registry = new(assemblies: [typeof(NoCatalogRule).Assembly]);

        MRuleAuthoringManifest manifest = registry.GetManifest(typeof(NoCatalogRule).Assembly)!;
        MRuleAuthoringEntry entry = manifest.Rules.Single(rule => rule.Code == "NO_CATALOG_ATTR");

        entry.DisplayName.Should().Be("NO_CATALOG_ATTR");
        entry.Tags.Should().BeEmpty();
        entry.IsPaletteVisible.Should().BeTrue();
    }

    /// <summary>
    /// Ensures hidden rules remain present in the manifest with visibility metadata preserved.
    /// </summary>
    [Fact]
    public void GetManifest_ShouldKeepHiddenRulesInManifest()
    {
        MRuleAuthoringManifestRegistry registry = new(assemblies: [typeof(CatalogHiddenRule).Assembly]);

        MRuleAuthoringManifest manifest = registry.GetManifest(typeof(CatalogHiddenRule).Assembly)!;
        MRuleAuthoringEntry entry = manifest.Rules.Single(rule => rule.Code == "CATALOG_HIDDEN");

        entry.DisplayName.Should().Be("Hidden Rule");
        entry.IsPaletteVisible.Should().BeFalse();
    }

    /// <summary>
    /// Test context for reflection schema discovery.
    /// </summary>
    public sealed class ManifestTestContext
    {
        /// <summary>
        /// Gets or sets the request payload.
        /// </summary>
        public ManifestRequest Request { get; set; } = new();
    }

    /// <summary>
    /// Test request payload for reflection schema discovery.
    /// </summary>
    public sealed class ManifestRequest
    {
        /// <summary>
        /// Gets or sets the order amount.
        /// </summary>
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Rule with visible palette metadata.
    /// </summary>
    [MRuleCatalogEntry(
        DisplayName = "Visible Rule",
        Category = "Shipping",
        Icon = "pi-ship",
        Tags = ["liner", "validation"],
        Description = "Checks liner metadata.")]
    public sealed class CatalogVisibleRule : IRule<ManifestTestContext>
    {
        /// <inheritdoc/>
        public string Code => "CATALOG_VISIBLE";

        /// <inheritdoc/>
        public Task<RuleResult> EvaluateAsync(ManifestTestContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }
    }

    /// <summary>
    /// Rule hidden from the authoring palette.
    /// </summary>
    [MRuleCatalogEntry(DisplayName = "Hidden Rule", IsPaletteVisible = false)]
    public sealed class CatalogHiddenRule : IRule<ManifestTestContext>
    {
        /// <inheritdoc/>
        public string Code => "CATALOG_HIDDEN";

        /// <inheritdoc/>
        public Task<RuleResult> EvaluateAsync(ManifestTestContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }
    }

    /// <summary>
    /// Rule without explicit catalog metadata.
    /// </summary>
    public sealed class NoCatalogRule : IRule<ManifestTestContext>
    {
        /// <inheritdoc/>
        public string Code => "NO_CATALOG_ATTR";

        /// <inheritdoc/>
        public Task<RuleResult> EvaluateAsync(ManifestTestContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }
    }
}
