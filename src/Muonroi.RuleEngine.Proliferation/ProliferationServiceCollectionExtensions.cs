using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        services.TryAddSingleton<IRuleProliferationBrain, OllamaProliferationBrain>();
        services.TryAddScoped<IScenarioExecutor, ScenarioExecutor>();
        services.TryAddSingleton<IProliferationStore, InMemoryProliferationStore>();
        services.AddHostedService<ProliferationWorker>();

        return services;
    }
}
