namespace Muonroi.RuleEngine.Proliferation;

/// <summary>
/// Service registration helpers for the proliferation engine.
/// </summary>
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
        services.TryAddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<ProliferationOptions>();

            // Composite brain: chain multiple providers
            if (!string.IsNullOrWhiteSpace(opts.CompositeBrains))
            {
                string[] brainNames = opts.CompositeBrains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (brainNames.Length > 1)
                {
                    List<IRuleProliferationBrain> brains = [.. brainNames.Select(name => CreateBrain(sp, name))];

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

        // Auth services for external project execution
        services.AddHttpClient("OAuth2Auth"); // Named client for OAuth2 token fetching
        services.AddHttpClient(nameof(ApiKeyRotationService)); // Named client for API key rotation
        services.TryAddSingleton<IOAuth2TokenProvider, CachingOAuth2TokenProvider>();
        services.TryAddSingleton<IMtlsHttpClientFactory, MtlsHttpClientFactory>();
        services.TryAddSingleton<IApiKeyRotationService, ApiKeyRotationService>();
        services.TryAddScoped<IAuthStrategyResolver, AuthStrategyResolver>();

        services.TryAddScoped<ScenarioExecutor>(); // Concrete internal executor
        services.TryAddScoped(sp =>
        {
            var connector = sp.GetRequiredService<Integration.Abstractions.IServiceTaskConnector>();
            var authResolver = sp.GetRequiredService<IAuthStrategyResolver>();
            var oauth2Provider = sp.GetRequiredService<IOAuth2TokenProvider>();
            var logFactory = sp.GetService<IMLogFactory>();
            return new ExternalScenarioExecutor(
                connector,
                authResolver,
                oauth2Provider,
                logFactory?.CreateLogger<ExternalScenarioExecutor>());
        });
        services.TryAddScoped<IScenarioExecutor>(sp =>
        {
            // Factory resolves concrete ScenarioExecutor as IScenarioExecutor to break circular dependency
            var internalExecutor = sp.GetRequiredService<ScenarioExecutor>();
            var externalExecutor = sp.GetRequiredService<ExternalScenarioExecutor>();
            var configProvider = sp.GetRequiredService<IExternalProjectConfigProvider>();
            var webhookService = sp.GetService<IWebhookNotificationService>();
            var logFactory = sp.GetService<IMLogFactory>();
            return new RoutingScenarioExecutor(
                internalExecutor,
                externalExecutor,
                configProvider,
                webhookService,
                logFactory?.CreateLogger<RoutingScenarioExecutor>());
        });
        services.TryAddSingleton<IProliferationStore, InMemoryProliferationStore>();
        services.AddHostedService<ProliferationWorker>();

        return services;
    }

    /// <summary>
    /// Creates the configured proliferation brain implementation.
    /// </summary>
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
