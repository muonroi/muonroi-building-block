namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Per-site service registration logic extracted from the scaffolding generator.
/// Called by generated partial RegisterServices() methods — handles DbContext registration
/// and behavior composition via runtime code instead of generated StringBuilder.
///
/// Uses reflection for AddSiteDbContext&lt;T&gt; to avoid a hard dependency on the Web/EF Core package
/// while still supporting it if present in the consumer project.
/// </summary>
public static class SiteProfileBootstrap
{
    private static readonly MethodInfo? s_addSiteDbContextMethod;

    /// <summary>
    /// Ambient logger set by <see cref="SiteProfileExtensions.AddMultiSiteProfilesCore"/> before
    /// calling <c>RegisterServices()</c> on each profile. Avoids <c>BuildServiceProvider()</c>
    /// anti-pattern inside DI registration. Null-safe — if not set, no logging occurs.
    /// <para>Thread-safe: DI registration is single-threaded by design.</para>
    /// </summary>
    internal static IMLog? CurrentLog { get; set; }

    static SiteProfileBootstrap()
    {
        // Resolve AddSiteDbContext<TContext>(IServiceCollection) via reflection to avoid hard dependency on Web package.
        // It's in Muonroi.Tenancy.SiteProfile.Web.SiteProfileDbContextExtensions.
        try
        {
            Type? extensionsType = Type.GetType("Muonroi.Tenancy.SiteProfile.Web.SiteProfileDbContextExtensions, Muonroi.Tenancy.SiteProfile.Web");
            if (extensionsType != null)
            {
                s_addSiteDbContextMethod = extensionsType.GetMethod("AddSiteDbContext", 1, new[] { typeof(IServiceCollection) });
            }
        }
        catch
        {
            // Fallback: Web package not present or different version
            s_addSiteDbContextMethod = null;
        }
    }

    /// <summary>
    /// Registers per-site services: DbContext (via AddSiteDbContext&lt;T&gt;) and behavior composition.
    /// Called from generated partial RegisterServices() method.
    /// <para>
    /// Logging uses <see cref="CurrentLog"/> (set by AddMultiSiteProfilesCore before calling RegisterServices).
    /// Does NOT call <c>BuildServiceProvider()</c> — avoids the anti-pattern of creating temporary
    /// ServiceProvider instances during DI registration.
    /// </para>
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
        var log = CurrentLog;

        log?.Info("[SiteProfile-AOT] RegisterSiteServices — begin (site: {SiteId})", siteId);

        // DbContext registration
        if (!skipDbContext && dbContextType is not null)
        {
            if (s_addSiteDbContextMethod != null)
            {
                // Call AddSiteDbContext<TContext>(services) via MakeGenericMethod — startup-only, not hot path
                MethodInfo genericMethod = s_addSiteDbContextMethod.MakeGenericMethod(dbContextType);
                genericMethod.Invoke(null, new object[] { services });
                log?.Info("[SiteProfile-AOT] Registered DbContext: {DbContextType}", dbContextType.FullName);
            }
            else
            {
                log?.Error(null, "[SiteProfile-AOT] DbContext registration requested for {DbContextType} but AddSiteDbContext extension method was not found. Ensure Muonroi.Tenancy.SiteProfile.Web is referenced.", 
                    dbContextType.FullName);
            }
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
                try
                {
                    if (Activator.CreateInstance(behaviorType) is ISiteProfileBehavior behavior)
                    {
                        behavior.Apply(services, configuration, siteId);
                        log?.Info("[SiteProfile-AOT] Applied behavior: {BehaviorType}", behaviorType.FullName);
                    }
                    else
                    {
                        log?.Warn("[SiteProfile-AOT] Failed to create behavior instance (not ISiteProfileBehavior): {BehaviorType}", behaviorType.FullName);
                    }
                }
                catch (Exception ex)
                {
                    log?.Error(ex, "[SiteProfile-AOT] Failed to create behavior instance: {BehaviorType}", 
                        behaviorType.FullName);
                }
            }
        }

        log?.Info("[SiteProfile-AOT] RegisterSiteServices — complete (site: {SiteId})", siteId);
    }
}
