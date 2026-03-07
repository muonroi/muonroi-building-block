using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Core.Helpers;
using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Serializers;
using Muonroi.RuleEngine.DecisionTable.Stores;
using Muonroi.RuleEngine.DecisionTable.Stores.Persistence;
using Muonroi.RuleEngine.DecisionTable.Validators;

namespace Muonroi.RuleEngine.DecisionTable;

public static class ServiceCollectionExtensions
{
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

        services.AddSingleton<DecisionTableValidator>();
        services.AddSingleton<OverlapDetector>();
        services.AddSingleton<GapDetector>();
        services.AddSingleton<IDecisionTableConverter, DecisionTableToJsonConverter>();
        services.AddSingleton<DecisionTableToRuleConverter>();
        services.AddSingleton<ExcelToDecisionTableConverter>();
        services.AddSingleton<DecisionTableJsonSerializer>();
        services.AddSingleton<DecisionTableXmlSerializer>();
        return services;
    }
}
