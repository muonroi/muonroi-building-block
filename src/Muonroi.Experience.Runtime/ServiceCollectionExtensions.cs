namespace Muonroi.Experience.Runtime;

/// <summary>
/// DI registration extensions for the Experience Engine runtime.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the experience store implementation and orchestrator with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional delegate to configure <see cref="ExperienceStoreOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddExperienceStore(
        this IServiceCollection services,
        Action<ExperienceStoreOptions>? configure = null)
    {
        var opts = new ExperienceStoreOptions();
        configure?.Invoke(opts);

        services.Configure<ExperienceStoreOptions>(o =>
        {
            o.StoreType = opts.StoreType;
            o.QdrantUrl = opts.QdrantUrl;
            o.FileDirectoryPath = opts.FileDirectoryPath;
            o.VectorSize = opts.VectorSize;
            o.Budget = opts.Budget;
        });

        if (opts.StoreType == ExperienceStoreType.Qdrant)
        {
            services.AddSingleton<QdrantClient>(_ =>
            {
                // QdrantClient constructor takes host + port, not a full URL.
                var uri = new Uri(opts.QdrantUrl);
                bool https = string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
                int port = uri.IsDefaultPort ? (https ? 443 : 6334) : uri.Port;
                return new QdrantClient(uri.Host, port, https);
            });
            services.AddSingleton<IQdrantClientWrapper, QdrantClientWrapper>();
            services.AddSingleton<IExperienceStore>(sp => new QdrantExperienceStore(
                sp.GetRequiredService<IQdrantClientWrapper>(),
                sp.GetRequiredService<IOptions<ExperienceStoreOptions>>(),
                sp.GetService<IMLog<QdrantExperienceStore>>(),
                sp.GetService<IExperienceBrain>()));
        }
        else
        {
            services.AddSingleton<IExperienceStore>(sp => new FileExperienceStore(
                sp.GetRequiredService<IOptions<ExperienceStoreOptions>>(),
                sp.GetService<IMLog<FileExperienceStore>>(),
                sp.GetService<IExperienceBrain>()));
        }

        services.AddSingleton<ExperienceStoreOrchestrator>();

        return services;
    }

    /// <summary>
    /// Registers the experience brain implementations, mistake detector, and extraction pipeline.
    /// Call after <see cref="AddExperienceStore"/> to wire the full extraction pipeline.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional delegate to configure <see cref="ExperienceBrainOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddExperienceBrain(
        this IServiceCollection services,
        Action<ExperienceBrainOptions>? configure = null)
    {
        var opts = new ExperienceBrainOptions();
        configure?.Invoke(opts);

        services.Configure<ExperienceBrainOptions>(o =>
        {
            o.ClaudeEndpoint = opts.ClaudeEndpoint;
            o.ClaudeApiKey = opts.ClaudeApiKey;
            o.ClaudeModel = opts.ClaudeModel;
            o.OllamaEndpoint = opts.OllamaEndpoint;
            o.OllamaPrimaryModel = opts.OllamaPrimaryModel;
            o.OllamaFallbackModel = opts.OllamaFallbackModel;
            o.AiTimeoutSeconds = opts.AiTimeoutSeconds;
            o.MaxTokens = opts.MaxTokens;
            o.Temperature = opts.Temperature;
        });

        services.AddHttpClient("ClaudeExperienceBrain");
        services.AddHttpClient("OllamaExperienceBrain");

        services.AddSingleton<ClaudeExperienceBrain>(sp =>
            new ClaudeExperienceBrain(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IOptions<ExperienceBrainOptions>>().Value,
                sp.GetService<IMLog<ClaudeExperienceBrain>>()));

        services.AddSingleton<OllamaExperienceBrain>(sp =>
            new OllamaExperienceBrain(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IOptions<ExperienceBrainOptions>>().Value,
                sp.GetService<IMLog<OllamaExperienceBrain>>()));

        services.AddSingleton<IExperienceBrain>(sp =>
            new CompositeExperienceBrain(
                sp.GetRequiredService<ClaudeExperienceBrain>(),
                sp.GetRequiredService<OllamaExperienceBrain>(),
                sp.GetService<IMLog<CompositeExperienceBrain>>()));

        services.AddSingleton<MistakeDetector>();
        services.AddSingleton<IExperienceExtractor, ExperienceExtractionPipeline>();

        return services;
    }

    /// <summary>
    /// Registers the evolution engine: <see cref="ExperienceEvolutionOrchestrator"/>,
    /// <see cref="IExperienceArchive"/>, and optionally <see cref="EvolutionBackgroundService"/>.
    /// Call after <see cref="AddExperienceStore"/> and <see cref="AddExperienceBrain"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional delegate to configure <see cref="EvolutionOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddExperienceEvolution(
        this IServiceCollection services,
        Action<EvolutionOptions>? configure = null)
    {
        var opts = new EvolutionOptions();
        configure?.Invoke(opts);

        // Register options singleton (used by orchestrator and background service)
        services.AddSingleton(opts);

        // Register IExperienceArchive based on store type configured in ExperienceStoreOptions
        services.AddSingleton<IExperienceArchive>(sp =>
        {
            ExperienceStoreOptions storeOpts = sp.GetRequiredService<IOptions<ExperienceStoreOptions>>().Value;

            if (storeOpts.StoreType == ExperienceStoreType.Qdrant)
            {
                return new QdrantExperienceArchive(
                    sp.GetRequiredService<IQdrantClientWrapper>(),
                    sp.GetRequiredService<IOptions<ExperienceStoreOptions>>(),
                    sp.GetService<IMLog<QdrantExperienceArchive>>());
            }

            return new FileExperienceArchive(
                sp.GetRequiredService<IOptions<ExperienceStoreOptions>>(),
                sp.GetService<IMLog<FileExperienceArchive>>());
        });

        // Register orchestrator as singleton
        services.AddSingleton<ExperienceEvolutionOrchestrator>(sp => new ExperienceEvolutionOrchestrator(
            sp.GetRequiredService<IExperienceStore>(),
            sp.GetRequiredService<IExperienceBrain>(),
            sp.GetRequiredService<IExperienceArchive>(),
            sp.GetRequiredService<EvolutionOptions>(),
            sp.GetRequiredService<IOptions<ExperienceStoreOptions>>().Value.Budget,
            sp.GetService<IMLog<ExperienceEvolutionOrchestrator>>()));

        // Optionally register background service
        if (opts.EnableBackgroundService)
        {
            services.AddSingleton<EvolutionBackgroundService>(sp => new EvolutionBackgroundService(
                sp.GetRequiredService<ExperienceEvolutionOrchestrator>(),
                sp.GetRequiredService<EvolutionOptions>(),
                sp.GetService<IMLog<EvolutionBackgroundService>>()));
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<EvolutionBackgroundService>());
        }

        return services;
    }
}
