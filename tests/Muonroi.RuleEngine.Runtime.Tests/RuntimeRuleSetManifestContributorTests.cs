using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Models;
using Muonroi.RuleEngine.Runtime.Web.Contributors;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuntimeRuleSetManifestContributorTests
{
    [Fact]
    public async Task ContributeAsync_ShouldRegisterRuntimeUiArtifacts()
    {
        RuntimeRuleSetManifestContributor contributor = new();
        MUiEngineManifest manifest = new();
        UiEngineManifestContext context = new()
        {
            Manifest = manifest,
            Services = new ServiceCollection().BuildServiceProvider()
        };

        await contributor.ContributeAsync(context);

        contributor.Order.Should().Be(140);
        contributor.ModuleId.Should().Be("runtime-ruleset");
        contributor.RequiredTier.Should().Be("Starter");

        manifest.ComponentRegistry.Components.Should().ContainKeys("runtime-ruleset-list", "runtime-ruleset-editor");
        manifest.Screens.Should().HaveCount(2);
        manifest.Actions.Should().HaveCount(6);
        manifest.DataSources.Should().HaveCount(2);
        manifest.NavigationGroups.Should().ContainSingle(x => x.GroupName == "rule-engine");

        MUiEngineScreen editorScreen = manifest.Screens.Single(x => x.UiKey == "runtime.ruleset.editor");
        editorScreen.ActionKeys.Should().Contain(MUiEngineKeyBuilder.BuildActionKey("runtime.ruleset.dryrun"));
        editorScreen.Components.Should().ContainSingle();
        editorScreen.Components[0].Props.Should().ContainKey("dryRunEndpointTemplate");
    }

    [Fact]
    public async Task ContributeAsync_ShouldAvoidDuplicateEntries_WhenCalledTwice()
    {
        RuntimeRuleSetManifestContributor contributor = new();
        MUiEngineManifest manifest = new();
        UiEngineManifestContext context = new()
        {
            Manifest = manifest,
            Services = new ServiceCollection().BuildServiceProvider()
        };

        await contributor.ContributeAsync(context);
        await contributor.ContributeAsync(context);

        manifest.Screens.Should().HaveCount(2);
        manifest.Actions.Should().HaveCount(6);
        manifest.DataSources.Should().HaveCount(2);
        manifest.NavigationGroups.Should().ContainSingle();
        manifest.NavigationGroups[0].Items.Should().ContainSingle();
    }
}
