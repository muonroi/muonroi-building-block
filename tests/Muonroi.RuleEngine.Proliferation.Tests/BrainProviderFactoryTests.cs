using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class BrainProviderFactoryTests
{
    private static IServiceProvider BuildProvider(string brainProvider)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Proliferation:BrainProvider"] = brainProvider,
                ["Proliferation:OllamaEndpoint"] = "http://localhost:11434",
                ["Proliferation:OpenAiEndpoint"] = "https://api.openai.com",
                ["Proliferation:OpenAiApiKey"] = "test-key",
                ["Proliferation:ClaudeEndpoint"] = "https://api.anthropic.com",
                ["Proliferation:ClaudeApiKey"] = "test-key"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddMProliferationEngine(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Resolves_OllamaBrain_ByDefault()
    {
        using ServiceProvider sp = (ServiceProvider)BuildProvider("ollama");
        IRuleProliferationBrain brain = sp.GetRequiredService<IRuleProliferationBrain>();
        brain.Should().BeOfType<OllamaProliferationBrain>();
    }

    [Fact]
    public void Resolves_OpenAiBrain()
    {
        using ServiceProvider sp = (ServiceProvider)BuildProvider("openai");
        IRuleProliferationBrain brain = sp.GetRequiredService<IRuleProliferationBrain>();
        brain.Should().BeOfType<OpenAiProliferationBrain>();
    }

    [Fact]
    public void Resolves_ClaudeBrain()
    {
        using ServiceProvider sp = (ServiceProvider)BuildProvider("claude");
        IRuleProliferationBrain brain = sp.GetRequiredService<IRuleProliferationBrain>();
        brain.Should().BeOfType<ClaudeProliferationBrain>();
    }

    [Fact]
    public void Resolves_OllamaBrain_ForUnknownProvider()
    {
        using ServiceProvider sp = (ServiceProvider)BuildProvider("unknown");
        IRuleProliferationBrain brain = sp.GetRequiredService<IRuleProliferationBrain>();
        brain.Should().BeOfType<OllamaProliferationBrain>();
    }

    [Fact]
    public void Resolves_PromptBuilder()
    {
        using ServiceProvider sp = (ServiceProvider)BuildProvider("ollama");
        IPromptBuilder builder = sp.GetRequiredService<IPromptBuilder>();
        builder.Should().BeOfType<DefaultPromptBuilder>();
    }
}
