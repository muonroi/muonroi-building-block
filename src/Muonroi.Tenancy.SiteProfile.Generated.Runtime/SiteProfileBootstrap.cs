using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Logging.Abstractions;
using Muonroi.Tenancy.SiteProfile.Web;

namespace Muonroi.Tenancy.SiteProfile.Generated.Runtime;

/// <summary>
/// Per-site service registration logic extracted from the scaffolding generator.
/// Called by generated partial RegisterServices() methods — handles DbContext registration
/// and behavior composition via runtime code instead of generated StringBuilder.
///
/// Uses MakeGenericMethod for AddSiteDbContext&lt;T&gt; — acceptable at startup (not hot path).
/// The key AOT win is that the manifest array creation (new() calls) happens in generated code.
/// </summary>
public static class SiteProfileBootstrap
{
    private static readonly MethodInfo s_addSiteDbContextMethod =
        typeof(SiteProfileDbContextExtensions)
            .GetMethod(nameof(SiteProfileDbContextExtensions.AddSiteDbContext), 1, [typeof(IServiceCollection)])
        ?? throw new MissingMethodException(
            nameof(SiteProfileDbContextExtensions),
            nameof(SiteProfileDbContextExtensions.AddSiteDbContext));

    /// <summary>
    /// Registers per-site services: DbContext (via AddSiteDbContext&lt;T&gt;) and behavior composition.
    /// Called from generated partial RegisterServices() method.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <param name="dbContextType">The DbContext type, or null if SkipDbContextRegistration.</param>
    /// <param name="behaviorTypes">Array of ISiteProfileBehavior types, or null if none.</param>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="skipDbContext">Whether to skip DbContext registration.</param>
    public static void RegisterSiteServices(
        string siteId,
        Type? dbContextType,
        Type[]? behaviorTypes,
        IServiceCollection services,
        IConfiguration configuration,
        bool skipDbContext = false)
    {
        var log = services.BuildServiceProvider()
            .GetService<IMLogFactory>()
            ?.CreateLogger($"Muonroi.SiteProfile.AOT.Bootstrap.{siteId}");

        log?.Info("[SiteProfile-AOT] RegisterSiteServices — begin (site: {SiteId})", siteId);

        // DbContext registration
        if (!skipDbContext && dbContextType is not null)
        {
            // Call AddSiteDbContext<TContext>(services) via MakeGenericMethod — startup-only, not hot path
            MethodInfo genericMethod = s_addSiteDbContextMethod.MakeGenericMethod(dbContextType);
            genericMethod.Invoke(null, [services]);
            log?.Info("[SiteProfile-AOT] Registered DbContext: {DbContextType}", dbContextType.FullName);
        }
        else
        {
            log?.Debug("[SiteProfile-AOT] DbContext registration skipped (site: {SiteId}, skipDbContext: {Skip})",
                siteId, skipDbContext);
        }

        // Behavior composition
        if (behaviorTypes is { Length: > 0 })
        {
            foreach (var behaviorType in behaviorTypes)
            {
                if (Activator.CreateInstance(behaviorType) is ISiteProfileBehavior behavior)
                {
                    behavior.Apply(services, configuration, siteId);
                    log?.Info("[SiteProfile-AOT] Applied behavior: {BehaviorType}", behaviorType.FullName);
                }
                else
                {
                    log?.Warn("[SiteProfile-AOT] Failed to create behavior instance: {BehaviorType}", behaviorType.FullName);
                }
            }
        }

        log?.Info("[SiteProfile-AOT] RegisterSiteServices — complete (site: {SiteId})", siteId);
    }
}
