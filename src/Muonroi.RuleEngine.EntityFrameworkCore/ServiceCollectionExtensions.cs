using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;
using Muonroi.RuleEngine.Abstractions.Adapters;
using Muonroi.RuleEngine.EntityFrameworkCore.Rules;
using Muonroi.RuleEngine.Runtime.Adapters;
using Muonroi.Tenancy.Abstractions;

namespace Muonroi.RuleEngine.EntityFrameworkCore;

/// <summary>
/// Dependency injection helpers for Entity Framework Core ruleset storage.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Postgres-backed ruleset storage, audit, approval, and canary services.
    /// </summary>
    public static IServiceCollection AddMRuleEngineWithPostgres(
        this IServiceCollection services,
        string connectionString,
        Action<RuleControlPlaneOptions>? configureOptions = null)
    {
        MGuard.NotNull(services);
        MGuard.NotEmpty(connectionString);
        services.EnsureFeatureOrThrow(FreeTierFeatures.Premium.RuleEngine);

        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        services.TryAddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.TryAddSingleton<IRuleSetDefinitionValidator, RuleSetDefinitionValidator>();
        services.TryAddSingleton<IMemoryCache, MemoryCache>();
        services.TryAddSingleton<IMJsonSerializeService, MJsonSerializeService>();
        services.TryAddSingleton(sp => CreateAuditSigner(sp.GetRequiredService<IOptions<RuleControlPlaneOptions>>().Value));

        // Graph parser and context adapters for flow graph execution
        services.TryAddSingleton<RuleGraphParser>();
        services.TryAddSingleton(typeof(IContextProjector<>), typeof(ReflectionContextProjector<>));
        services.TryAddSingleton(typeof(IContextFactory<>), typeof(ReflectionContextFactory<>));
        services.TryAddSingleton<IRuleSetChangeNotifier, InMemoryRuleSetChangeNotifier>();

        // Register RLS interceptor as a singleton so it can be injected into DbContext options.
        services.AddOptions<MultiTenantOptions>().BindConfiguration(MultiTenantOptions.SectionName);
        services.TryAddSingleton(sp =>
            new TenantRlsConnectionInterceptor(sp.GetRequiredService<IOptions<MultiTenantOptions>>()));

        services.AddDbContext<RuleEngineDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            MultiTenantOptions multiTenantOptions = sp.GetRequiredService<IOptions<MultiTenantOptions>>().Value;
            if (multiTenantOptions.EnableRowLevelSecurity)
            {
                options.AddInterceptors(sp.GetRequiredService<TenantRlsConnectionInterceptor>());
            }
        });

        services.Replace(ServiceDescriptor.Scoped<IRuleSetStore, PostgresRuleSetStore>());
        services.Replace(ServiceDescriptor.Scoped<IRuleSetAuditStore, PostgresRuleSetAuditStore>());
        services.TryAddScoped<IRuleSetApprovalService, RuleSetApprovalService>();
        services.TryAddScoped<ICanaryRolloutService, CanaryRolloutService>();
        services.TryAddScoped<RulesEngineService>();

        return services;
    }

    /// <summary>
    /// Enables maker-checker approval workflow for rulesets.
    /// </summary>
    public static IServiceCollection AddMRuleEngineApprovalWorkflow(this IServiceCollection services)
    {
        MGuard.NotNull(services);
        services.Configure<RuleControlPlaneOptions>(options => options.RequireApproval = true);
        services.TryAddScoped<IRuleSetApprovalService, RuleSetApprovalService>();
        return services;
    }

    /// <summary>
    /// Enables tenant-targeted canary rollout services.
    /// </summary>
    public static IServiceCollection AddMCanaryRollout(this IServiceCollection services)
    {
        MGuard.NotNull(services);
        services.Configure<RuleControlPlaneOptions>(options => options.EnableCanary = true);
        services.TryAddScoped<ICanaryRolloutService, CanaryRolloutService>();
        return services;
    }

    private static IRuleSetAuditSigner CreateAuditSigner(RuleControlPlaneOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AuditPrivateKeyPem))
        {
            return RsaRuleSetAuditSigner.FromPrivateKeyPem(options.AuditPrivateKeyPem, options.AuditSignerKeyId);
        }

        if (!string.IsNullOrWhiteSpace(options.AuditPrivateKeyPemPath))
        {
            return RsaRuleSetAuditSigner.FromPrivateKeyFile(options.AuditPrivateKeyPemPath, options.AuditSignerKeyId);
        }

        return RsaRuleSetAuditSigner.CreateEphemeral(options.AuditSignerKeyId);
    }
}
