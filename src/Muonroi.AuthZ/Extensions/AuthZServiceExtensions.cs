namespace Muonroi.AuthZ.Extensions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.AuthZ.Authorization;
using Muonroi.AuthZ.HotReload;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;

/// <summary>
/// Service registration helpers for rule-driven authorization.
/// </summary>
public static class AuthZServiceExtensions
{
    /// <summary>
    /// Registers the rule-engine-driven authorization evaluator.
    /// After calling this, register authorization rules as:
    ///   services.AddScoped&lt;IRule&lt;AuthorizationRuleContext&gt;, YourRule&gt;();
    /// </summary>
    public static IServiceCollection AddMAuthorizationRuleEngine(
        this IServiceCollection services)
    {
        MGuard.NotNull(services);

        // Register orchestrator for AuthorizationRuleContext so the DI chain resolves.
        // Wraps RuleOrchestrator<AuthorizationRuleContext> which has no IRuleContext constraint.
        services.TryAddScoped<IMRuleOrchestrator<AuthorizationRuleContext>>(sp =>
        {
            RuleOrchestrator<AuthorizationRuleContext> inner = new(
                sp.GetServices<IRule<AuthorizationRuleContext>>(),
                sp.GetServices<IHookHandler<AuthorizationRuleContext>>(),
                sp.GetService<IMLog<RuleOrchestrator<AuthorizationRuleContext>>>());
            return new AuthorizationRuleOrchestratorAdapter(inner);
        });

        services.AddSingleton<IAuthorizationPolicyEvaluator, RuleEngineAuthorizationPolicyEvaluator>();
        services.AddScoped<IAuthorizationHandler, MuonroiAuthorizationHandler>();
        services.AddScoped(typeof(Muonroi.AuthZ.RowSecurity.IRuleRowFilter<>), typeof(Muonroi.AuthZ.RowSecurity.RuleRowFilter<>));

        // Register the default hot-reload change handler. Consumer apps can override by
        // registering their own IAuthRuleChangeHandler before calling this method.
        services.TryAddSingleton<IAuthRuleChangeHandler, DefaultAuthRuleChangeHandler>();

        return services;
    }

    /// <summary>
    /// Adds a named policy "rule-engine:{resource}:{action}" backed by MuonroiAuthorizationRequirement.
    /// Usage: .RequireRuleEngineAuthorization("orders", "read")
    /// </summary>
    public static IEndpointConventionBuilder RequireRuleEngineAuthorization(
        this IEndpointConventionBuilder builder, string resource, string action)
    {
        return builder.RequireAuthorization(
            policy => policy.AddRequirements(new MuonroiAuthorizationRequirement(resource, action)));
    }

    /// <summary>
    /// Enables SignalR-based authorization rule hot-reload from Control Plane.
    /// </summary>
    public static IServiceCollection AddMAuthorizationHotReload(
        this IServiceCollection services,
        Action<AuthRuleHotReloadOptions> configure)
    {
        MGuard.NotNull(services);
        MGuard.NotNull(configure);

        AuthRuleHotReloadOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddHostedService<AuthRuleHotReloadClient>();

        return services;
    }
}
