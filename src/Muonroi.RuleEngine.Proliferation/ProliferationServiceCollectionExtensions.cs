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

        // Synthetic scenario generator (AI fallback)
        services.TryAddSingleton<ISyntheticScenarioGenerator, SyntheticScenarioGenerator>();

        // Infrastructure health monitor (optional, gated by EnableInfraAwareBudget)
        if (options.EnableInfraAwareBudget)
        {
            services.TryAddSingleton<IInfraHealthMonitor>(sp =>
            {
                var opts = sp.GetRequiredService<ProliferationOptions>();
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var logFactory = sp.GetService<IMLogFactory>();
                return new InfraHealthMonitor(factory, opts, logFactory?.CreateLogger<InfraHealthMonitor>());
            });
        }

        // Brain provider factory — single or composite
        services.TryAddSingleton<IRuleProliferationBrain>(sp =>
        {
            var opts = sp.GetRequiredService<ProliferationOptions>();

            // Composite brain: chain multiple providers
            if (!string.IsNullOrWhiteSpace(opts.CompositeBrains))
            {
                string[] brainNames = opts.CompositeBrains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (brainNames.Length > 1)
                {
                    List<IRuleProliferationBrain> brains = brainNames
                        .Select(name => CreateBrain(sp, name))
                        .ToList();

                    CompositeMode mode = opts.CompositeMode.Equals("sequential", StringComparison.OrdinalIgnoreCase)
                        ? CompositeMode.Sequential
                        : CompositeMode.Parallel;

                    var dedup = sp.GetService<IScenarioDeduplicator>();
                    var logFactory = sp.GetService<IMLogFactory>();
                    return new CompositeProliferationBrain(brains, mode, dedup,
                        logFactory?.CreateLogger<CompositeProliferationBrain>());
                }
            }

            return CreateBrain(sp, opts.BrainProvider);
        });

        // Vector embedder for semantic dedup
        services.TryAddSingleton<IVectorEmbedder>(sp =>
        {
            var opts = sp.GetRequiredService<ProliferationOptions>();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logFactory = sp.GetService<IMLogFactory>();
            return new OllamaEmbedder(factory, opts, logFactory?.CreateLogger<OllamaEmbedder>());
        });

        // Failure analyzer and deduplicator
        services.TryAddSingleton<IFailureAnalyzer>(sp =>
        {
            var opts = sp.GetRequiredService<ProliferationOptions>();
            var brainInstance = sp.GetRequiredService<IRuleProliferationBrain>();
            var logFactory = sp.GetService<IMLogFactory>();
            return new DefaultFailureAnalyzer(brainInstance, opts,
                logFactory?.CreateLogger<DefaultFailureAnalyzer>());
        });

        // Deduplicator: semantic (hash+vector) or hash-only
        services.TryAddSingleton<InputHashDeduplicator>();
        services.TryAddSingleton<IScenarioDeduplicator>(sp =>
        {
            var opts = sp.GetRequiredService<ProliferationOptions>();
            var hashDedup = sp.GetRequiredService<InputHashDeduplicator>();

            if (opts.EnableSemanticDedup)
            {
                var embedder = sp.GetRequiredService<IVectorEmbedder>();
                var logFactory = sp.GetService<IMLogFactory>();
                return new VectorSemanticDeduplicator(embedder, opts, hashDedup,
                    logFactory?.CreateLogger<VectorSemanticDeduplicator>());
            }

            return hashDedup;
        });

        // Natural language rule converter
        services.TryAddSingleton<INaturalLanguageRuleConverter, NaturalLanguageRuleConverter>();

        // Chaos scenario generator
        services.TryAddSingleton<IChaosScenarioGenerator, DefaultChaosScenarioGenerator>();

        // Smart budget allocator
        services.TryAddSingleton<IBudgetAllocator, CoverageWeightedBudgetAllocator>();

        services.TryAddSingleton<ICoverageTracker, DefaultCoverageTracker>();
        services.TryAddScoped<IRegressionRunner, DefaultRegressionRunner>();

        services.TryAddScoped<ScenarioExecutor>(); // Register concrete type for injection into RoutingScenarioExecutor
        services.TryAddScoped<ExternalScenarioExecutor>(); // Register concrete type for injection into RoutingScenarioExecutor
        services.TryAddScoped<IScenarioExecutor, RoutingScenarioExecutor>(); // Decorator wraps both executors
        services.TryAddSingleton<IProliferationStore, InMemoryProliferationStore>();
        services.AddHostedService<ProliferationWorker>();

        return services;
    }

    private static IRuleProliferationBrain CreateBrain(IServiceProvider sp, string? providerName)
    {
        var opts = sp.GetRequiredService<ProliferationOptions>();
        var prompt = sp.GetRequiredService<IPromptBuilder>();
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var logFactory = sp.GetService<IMLogFactory>();
        var syntheticGen = sp.GetService<ISyntheticScenarioGenerator>();
        var infraMonitor = sp.GetService<IInfraHealthMonitor>();

        return providerName?.ToLowerInvariant() switch
        {
            "openai" => new OpenAiProliferationBrain(factory, opts, prompt,
                logFactory?.CreateLogger<OpenAiProliferationBrain>(), syntheticGen),
            "claude" => new ClaudeProliferationBrain(factory, opts, prompt,
                logFactory?.CreateLogger<ClaudeProliferationBrain>(), syntheticGen),
            _ => new OllamaProliferationBrain(factory, opts, prompt,
                logFactory?.CreateLogger<OllamaProliferationBrain>(), syntheticGen, infraMonitor)
        };
    }
}
