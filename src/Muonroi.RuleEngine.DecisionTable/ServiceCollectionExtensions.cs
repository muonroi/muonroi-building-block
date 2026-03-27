using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Core.Helpers;
using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Feel;
using Muonroi.RuleEngine.DecisionTable.Serializers;
using Muonroi.RuleEngine.DecisionTable.Stores;
using Muonroi.RuleEngine.DecisionTable.Stores.Persistence;
using Muonroi.RuleEngine.DecisionTable.Validators;

namespace Muonroi.RuleEngine.DecisionTable;

/// <summary>
/// Dependency injection helpers for the decision table engine.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers decision table services and storage.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Optional configuration action for engine options.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDecisionTableEngine(
        this IServiceCollection services,
        Action<DecisionTableEngineOptions>? configure = null)
    {
        DecisionTableEngineOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(Options.Create(options));
        services.TryAddSingleton<IMJsonSerializeService, MJsonSerializeService>();
        services.TryAddSingleton<IMDateTimeService, MDateTimeService>();

        if (!string.IsNullOrWhiteSpace(options.PostgresConnectionString))
        {
            services.AddDbContext<DecisionTableDbContext>((_, db) =>
            {
                db.UseNpgsql(options.PostgresConnectionString);
            });
            services.AddScoped<IDecisionTableStore, EfCoreDecisionTableStore>();
            services.AddHostedService<DecisionTableDatabaseMigrator>();
        }
        else if (!string.IsNullOrWhiteSpace(options.SqlServerConnectionString))
        {
            services.AddDbContext<DecisionTableDbContext>((_, db) =>
            {
                db.UseSqlServer(options.SqlServerConnectionString);
            });
            services.AddScoped<IDecisionTableStore, EfCoreDecisionTableStore>();
            services.AddHostedService<DecisionTableDatabaseMigrator>();
        }
        else
        {
            services.AddSingleton<IDecisionTableStore, InMemoryDecisionTableStore>();
        }

        services.AddSingleton<SimplifiedFeelCellEvaluator>();
        services.AddSingleton<IFeelCellEvaluator, FullFeelCellEvaluator>();
        services.AddSingleton<DecisionTableValidator>();
        services.AddSingleton<OverlapDetector>();
        services.AddSingleton<MultiColumnOverlapDetector>();
        services.AddSingleton<RedundancyDetector>();
        services.AddSingleton<GapDetector>();
        services.AddSingleton<IDecisionTableExecutor, DecisionTableExecutor>();
        services.AddSingleton<DecisionTableDiffer>();
        services.AddSingleton<IDecisionTableConverter, DecisionTableToJsonConverter>();
        services.AddSingleton<DecisionTableToRuleConverter>();
        services.AddSingleton<ExcelToDecisionTableConverter>();
        services.AddSingleton<DecisionTableJsonSerializer>();
        services.AddSingleton<DecisionTableXmlSerializer>();

        // DMN 1.3 import/export are available as static utility classes:
        //   DmnExporter.ExportToDmnXml(table)    — model → DMN 1.3 XML
        //   DmnImporter.ImportFromDmnXml(xml)    — DMN 1.3 XML → model
        //   DmnHitPolicyMapper.ToDmnString(...)  — bidirectional hit policy mapping
        return services;
    }
}
