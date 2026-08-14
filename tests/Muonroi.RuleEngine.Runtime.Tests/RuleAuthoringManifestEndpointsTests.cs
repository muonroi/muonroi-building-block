namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Covers authoring manifest endpoint behavior.
/// </summary>
public sealed class RuleAuthoringManifestEndpointsTests
{
    /// <summary>
    /// Ensures the endpoint returns a merged manifest payload when rules are available.
    /// </summary>
    [Fact]
    public async Task MapRuleAuthoringManifestEndpoint_ShouldReturnManifestPayload()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new MRuleAuthoringManifestRegistry(assemblies: [typeof(MRuleAuthoringManifestRegistryTests.CatalogVisibleRule).Assembly]));

        await using WebApplication app = builder.Build();
        app.MapRuleAuthoringManifestEndpoint();
        await app.StartAsync();

        MRuleAuthoringManifest? payload = await app.GetTestClient().GetFromJsonAsync<MRuleAuthoringManifest>("/api/internal/rule-authoring/manifest");

        payload.Should().NotBeNull();
        payload!.Rules.Should().Contain(rule => rule.Code == "CATALOG_VISIBLE");
    }

    /// <summary>
    /// Ensures the endpoint returns 404 when no rules are available.
    /// </summary>
    [Fact]
    public async Task MapRuleAuthoringManifestEndpoint_ShouldReturnNotFoundWhenRegistryIsEmpty()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new MRuleAuthoringManifestRegistry(assemblies: Array.Empty<System.Reflection.Assembly>()));

        await using WebApplication app = builder.Build();
        app.MapRuleAuthoringManifestEndpoint();
        await app.StartAsync();

        HttpResponseMessage response = await app.GetTestClient().GetAsync("/api/internal/rule-authoring/manifest");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Ensures merge behavior deduplicates by rule code and preserves ordering.
    /// </summary>
    [Fact]
    public void MergeManifests_ShouldDeduplicateByCodeAndOrderBySequence()
    {
        MRuleAuthoringManifest merged = RuleAuthoringManifestEndpoints.MergeManifests(
        [
            new MRuleAuthoringManifest
            {
                AssemblyName = "A",
                AssemblyVersion = "1.0.0",
                Rules =
                [
                    new MRuleAuthoringEntry { Code = "RULE_B", Order = 2, DisplayName = "Rule B" },
                    new MRuleAuthoringEntry { Code = "RULE_A", Order = 1, DisplayName = "Rule A" }
                ]
            },
            new MRuleAuthoringManifest
            {
                AssemblyName = "B",
                AssemblyVersion = "1.0.0",
                Rules =
                [
                    new MRuleAuthoringEntry { Code = "RULE_A", Order = 99, DisplayName = "Duplicate A" }
                ]
            }
        ])!;

        merged.Rules.Select(rule => rule.Code).Should().ContainInOrder("RULE_A", "RULE_B");
        merged.Rules.Should().ContainSingle(rule => rule.Code == "RULE_A" && rule.DisplayName == "Rule A");
    }
}
