using Muonroi.Rules.Contributors;

namespace Muonroi.Rules.Tests.Feel;

public sealed class FeelPlaygroundManifestContributorTests
{
    [Fact]
    public async Task ContributeAsync_PopulatesManifest_AndIsIdempotent()
    {
        FeelPlaygroundManifestContributor contributor = new();
        UiEngineManifestContext context = new()
        {
            Manifest = new MUiEngineManifest(),
            Services = new ServiceCollection().BuildServiceProvider()
        };

        await contributor.ContributeAsync(context);
        await contributor.ContributeAsync(context);

        contributor.Order.Should().Be(130);
        contributor.ModuleId.Should().Be("feel-playground");
        contributor.RequiredTier.Should().Be("Starter");

        context.Manifest.ComponentRegistry.Components.Should().ContainKey("feel-playground");
        context.Manifest.Screens.Should().ContainSingle(x => x.Route == "/rule-engine/feel");
        context.Manifest.Actions.Should().ContainSingle(x => x.Route == "/api/v1/feel/evaluate");
        context.Manifest.DataSources.Should().ContainSingle(x => x.EndpointPath == "/api/v1/feel/examples");
        context.Manifest.NavigationGroups.Should().ContainSingle(x => x.GroupName == "rule-engine");

        MUiEngineScreen screen = context.Manifest.Screens.Single();
        screen.Components.Should().ContainSingle();
        screen.Components[0].Props["evaluateEndpoint"].Should().Be("/api/v1/feel/evaluate");
        screen.Components[0].Props["autocompleteEndpoint"].Should().Be("/api/v1/feel/autocomplete");
    }
}
