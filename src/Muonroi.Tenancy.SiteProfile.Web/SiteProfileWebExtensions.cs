using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.SiteProfile;
using Muonroi.Tenancy.SiteProfile.Web.HotReload;
using Muonroi.Tenancy.SiteProfile.Web.Telemetry;

namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Extension methods for registering SiteProfile Web components (middleware + hot-reload).
/// </summary>
public static class SiteProfileWebExtensions
{
    /// <summary>
    /// Adds SiteProfile state registry and hot-reload client as a hosted service.
    /// The state registry is the mutable bridge between hot-reload events and middleware.
    /// </summary>
    public static IServiceCollection AddSiteProfileHotReload(
        this IServiceCollection services,
        Action<SiteProfileHotReloadOptions> configure)
    {
        var options = new SiteProfileHotReloadOptions();
        configure(options);
        services.AddSingleton(options);
        services.AddSingleton<ISiteProfileStateRegistry, SiteProfileStateRegistry>();
        services.AddHostedService<SiteProfileHotReloadClient>();
        return services;
    }

    /// <summary>
    /// Adds middleware that returns 503 for disabled sites. Place after UseRouting and any tenant resolution middleware.
    /// </summary>
    public static IApplicationBuilder UseSiteProfileStateMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SiteProfileStateMiddleware>();
    }

    /// <summary>
    /// Registers ASP.NET Core ApplicationParts from site assemblies so that controllers
    /// defined in Core or per-site projects are discovered by the MVC framework.
    /// Mirrors the <c>MapSiteGrpcServices()</c> pattern for REST controllers.
    ///
    /// <para>
    /// Call AFTER <c>AddControllers()</c> / <c>AddApplication()</c> in Program.cs.
    /// Controllers in site assemblies will be automatically routed alongside host controllers.
    /// </para>
    ///
    /// <code>
    /// // Program.cs — after AddApplication:
    /// services.AddSiteControllers(
    ///     typeof(TciSiteProfile).Assembly,      // Sites/TCI — TCI-specific controllers
    ///     typeof(DefaultSiteProfile).Assembly,   // Sites/Default
    ///     typeof(CoreHandlers).Assembly);         // Core — shared controllers
    /// </code>
    /// </summary>
    /// <param name="services">The service collection (must have MVC already registered).</param>
    /// <param name="siteAssemblies">
    /// Assemblies containing controllers to discover. Typically the assembly of each ISiteProfile
    /// and the Core project assembly.
    /// </param>
    /// <returns>The <see cref="IMvcBuilder"/> for further chaining.</returns>
    public static IMvcBuilder AddSiteControllers(
        this IServiceCollection services,
        params Assembly[] siteAssemblies)
    {
        var mvcBuilder = services.AddControllers();
        foreach (Assembly asm in siteAssemblies.Distinct())
        {
            mvcBuilder.AddApplicationPart(asm);
        }
        return mvcBuilder;
    }

    /// <summary>
    /// One-call setup for the entire multi-site infrastructure. Replaces 5-7 individual ecosystem calls
    /// that every consumer copy-pastes. Consumer only provides what's project-specific via options.
    ///
    /// <para>
    /// Internally calls: <c>AddMultiSiteProfiles</c>, <c>AddSiteConfiguration</c>,
    /// <c>AddSiteControllers</c> (if enabled), <c>SkipSiteProfileStartupValidation</c> (if enabled).
    /// </para>
    ///
    /// <code>
    /// // Before — 7 individual calls per consumer:
    /// services.AddMultiSiteProfiles(config, accessor, assemblies);
    /// services.AddSiteConfiguration();
    /// services.AddSiteControllers(assemblies);
    /// services.SkipSiteProfileStartupValidation();
    ///
    /// // After — 1 call:
    /// services.AddSiteInfrastructure(configuration, options =>
    /// {
    ///     options.SiteCodeAccessor = sp => sp.GetRequiredService&lt;IWorkContextAccessor&gt;().WorkContext?.SiteCode;
    ///     options.SiteAssemblies = [typeof(TciSiteProfile).Assembly, typeof(DefaultSiteProfile).Assembly];
    ///     options.EnableControllerDiscovery = true;  // aggregate projects
    ///     options.SkipStartupValidation = true;      // no DbContext validation
    /// });
    /// </code>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration (forwarded to site profiles).</param>
    /// <param name="configure">Configure project-specific options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSiteInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<SiteInfrastructureOptions> configure)
    {
        var options = new SiteInfrastructureOptions();
        configure(options);

        if (options.SiteCodeAccessor is null)
            throw new InvalidOperationException(
                "SiteCodeAccessor is required. Set it in AddSiteInfrastructure options.");

        if (options.SiteAssemblies.Length == 0)
            throw new InvalidOperationException(
                "SiteAssemblies is required. Provide at least one assembly containing ISiteProfile implementations.");

        // 1. Register all site profiles with per-request resolution
        services.AddMultiSiteProfiles(
            configuration,
            options.SiteCodeAccessor,
            diagnosticLog: null,
            options.SiteAssemblies);

        // 2. Per-site configuration overlay (reads Sites:{SiteId}:* from appsettings)
        services.AddSiteConfiguration();

        // 3. Controller discovery from site assemblies (aggregate projects)
        if (options.EnableControllerDiscovery)
        {
            services.AddSiteControllers(options.SiteAssemblies);
        }

        // 4. Skip startup validation (aggregate projects without DbContext)
        if (options.SkipStartupValidation)
        {
            services.SkipSiteProfileStartupValidation();
        }

        return services;
    }

    /// <summary>
    /// Adds middleware that enriches each request with site-scoped observability:
    /// Activity.Current site.id tag, IMLog SiteId scope, and site_profile_requests_total counter.
    ///
    /// Place after <see cref="UseSiteProfileStateMiddleware"/> in the pipeline.
    /// Requires <see cref="Muonroi.Tenancy.SiteProfile.ISiteProfileResolver"/> to be registered (via AddMultiSiteProfiles).
    /// When resolver is absent, the middleware is a no-op passthrough.
    /// </summary>
    public static IApplicationBuilder UseSiteProfileTelemetry(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SiteProfileTelemetryMiddleware>();
    }

    /// <summary>
    /// Applies all <see cref="SiteProfileBehaviorAttribute"/> behaviors declared on <paramref name="profile"/>.
    /// Instantiates each behavior type via parameterless constructor and calls Apply() with the profile's SiteId.
    ///
    /// Usage — after AddMultiSiteProfiles or AddSiteProfile:
    /// <code>
    /// services.AddSiteProfile&lt;TciSiteProfile&gt;(config)
    ///         .AddSiteProfileBehaviors(new TciSiteProfile(), config);
    /// </code>
    ///
    /// Or during AddMultiSiteProfiles scanning — call per-profile inside RegisterServices.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="profile">The ISiteProfile instance whose [SiteProfileBehavior] attributes to apply.</param>
    /// <param name="configuration">Application configuration forwarded to each behavior's Apply().</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a behavior type does not have a parameterless constructor or does not implement ISiteProfileBehavior.
    /// </exception>
    public static IServiceCollection AddSiteProfileBehaviors(
        this IServiceCollection services,
        ISiteProfile profile,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(configuration);

        var behaviorAttributes = profile.GetType()
            .GetCustomAttributes<SiteProfileBehaviorAttribute>(inherit: false);

        foreach (var attr in behaviorAttributes)
        {
            if (!typeof(ISiteProfileBehavior).IsAssignableFrom(attr.BehaviorType))
                throw new InvalidOperationException(
                    $"Type '{attr.BehaviorType.FullName}' does not implement ISiteProfileBehavior.");

            var behavior = (ISiteProfileBehavior?)Activator.CreateInstance(attr.BehaviorType)
                ?? throw new InvalidOperationException(
                    $"Failed to create instance of behavior type '{attr.BehaviorType.FullName}'. " +
                    "Ensure the type has a public parameterless constructor.");

            behavior.Apply(services, configuration, profile.SiteId);
        }

        return services;
    }
}
