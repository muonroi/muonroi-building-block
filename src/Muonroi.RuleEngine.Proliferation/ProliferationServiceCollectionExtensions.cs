using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Execution;
using Muonroi.RuleEngine.Proliferation.Store;
using Muonroi.RuleEngine.Proliferation.Worker;

namespace Muonroi.RuleEngine.Proliferation;

public static class ProliferationServiceCollectionExtensions
{
    /// <summary>
    /// Register core Proliferation Engine services (brain, executor, worker) with in-memory store.
    /// For Postgres persistence, call AddMProliferationPostgres() from the Persistence package.
    /// </summary>
    public static IServiceCollection AddMProliferationEngine(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ProliferationOptions options = new();
        configuration.GetSection(ProliferationOptions.SectionName).Bind(options);
        services.TryAddSingleton(options);

        // HTTP clients per provider
        services.AddHttpClient("OllamaProliferation", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(options.AiTimeoutSeconds + 30);
        });

        services.AddHttpClient("OpenAiProliferation", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(options.AiTimeoutSeconds + 30);
        });

        services.AddHttpClient("ClaudeProliferation", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(options.AiTimeoutSeconds + 30);
        });

        // Shared prompt builder
        services.TryAddSingleton<IPromptBuilder, DefaultPromptBuilder>();

        // Brain provider factory — resolved by BrainProvider option
        services.TryAddSingleton<IRuleProliferationBrain>(sp =>
        {
            var opts = sp.GetRequiredService<ProliferationOptions>();
            var prompt = sp.GetRequiredService<IPromptBuilder>();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logFactory = sp.GetService<IMLogFactory>();

            return opts.BrainProvider?.ToLowerInvariant() switch
            {
                "openai" => new OpenAiProliferationBrain(
                    factory, opts, prompt,
                    logFactory?.CreateLogger<OpenAiProliferationBrain>()),
                "claude" => new ClaudeProliferationBrain(
                    factory, opts, prompt,
                    logFactory?.CreateLogger<ClaudeProliferationBrain>()),
                _ => new OllamaProliferationBrain(
                    factory, opts, prompt,
                    logFactory?.CreateLogger<OllamaProliferationBrain>())
            };
        });

        services.TryAddScoped<IScenarioExecutor, ScenarioExecutor>();
        services.TryAddSingleton<IProliferationStore, InMemoryProliferationStore>();
        services.AddHostedService<ProliferationWorker>();

        return services;
    }
}
