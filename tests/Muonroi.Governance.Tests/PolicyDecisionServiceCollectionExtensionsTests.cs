using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Muonroi.Governance.Authorization;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Governance.Tests;

public sealed class PolicyDecisionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMPolicyDecision_ShouldBindConfigs_AndRegisterService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MPolicyDecisionConfigs.SectionName}:Enabled"] = "true",
                [$"{MPolicyDecisionConfigs.SectionName}:Provider"] = nameof(MPolicyDecisionProvider.OpenFga),
                [$"{MPolicyDecisionConfigs.SectionName}:Endpoint"] = "https://pdp.example",
                [$"{MPolicyDecisionConfigs.SectionName}:TimeoutSeconds"] = "9",
                [$"{MPolicyDecisionConfigs.SectionName}:DefaultHeaders:Authorization"] = "Bearer test-token",
                [$"{MPolicyDecisionConfigs.SectionName}:DecisionPath"] = "/custom-check"
            })
            .Build();

        services.AddMPolicyDecision(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IMPolicyDecisionService>().Should().BeOfType<MPolicyDecisionService>();

        MPolicyDecisionConfigs configs = provider.GetRequiredService<MPolicyDecisionConfigs>();
        configs.Enabled.Should().BeTrue();
        configs.Provider.Should().Be(MPolicyDecisionProvider.OpenFga);
        configs.Endpoint.Should().Be("https://pdp.example");
        configs.TimeoutSeconds.Should().Be(9);
        configs.DecisionPath.Should().Be("/custom-check");
        configs.DefaultHeaders["Authorization"].Should().Be("Bearer test-token");
    }

    [Fact]
    public void AddMPolicyDecision_ShouldConfigureNamedHttpClient()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MPolicyDecisionConfigs.SectionName}:Endpoint"] = "https://pdp.example",
                [$"{MPolicyDecisionConfigs.SectionName}:TimeoutSeconds"] = "7",
                [$"{MPolicyDecisionConfigs.SectionName}:DefaultHeaders:X-Api-Key"] = "abc123",
                [$"{MPolicyDecisionConfigs.SectionName}:DefaultHeaders:"] = ""
            })
            .Build();

        services.AddMPolicyDecision(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        HttpClient client = factory.CreateClient("MuonroiPolicyDecision");

        client.BaseAddress.Should().Be(new Uri("https://pdp.example"));
        client.Timeout.Should().Be(TimeSpan.FromSeconds(7));
        client.DefaultRequestHeaders.Contains("X-Api-Key").Should().BeTrue();
        client.DefaultRequestHeaders.GetValues("X-Api-Key").Single().Should().Be("abc123");
    }

    [Fact]
    public void AddMPolicyDecision_WhenArgumentsNull_ShouldThrow()
    {
        ServiceCollection? services = null;
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Assert.Throws<MArgumentException>(() => services!.AddMPolicyDecision(configuration));
        Assert.Throws<MArgumentException>(() => new ServiceCollection().AddMPolicyDecision(null!));
    }
}
