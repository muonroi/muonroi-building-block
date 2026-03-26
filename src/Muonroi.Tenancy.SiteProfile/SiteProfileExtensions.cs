using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Extension methods for ISiteProfile registration.
/// </summary>
public static class SiteProfileExtensions
{
    // Static tracker — populated during DI setup (single-threaded), consumed read-only at startup.
    private static readonly SiteProfileRegistrationTracker s_tracker = new();

    /// <summary>
    /// Register all services for a specific site profile.
    /// One-liner in Program.cs: services.AddSiteProfile&lt;Sg01Profile&gt;(config)
    /// Use when 1 binary = 1 site (deploy per site).
    /// </summary>
    public static IServiceCollection AddSiteProfile<TProfile>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TProfile : class, ISiteProfile, new()
    {
        var profile = new TProfile();
        services.AddSingleton<ISiteProfile>(profile);
        profile.RegisterServices(services, configuration);

        // Track site ID for startup validation
        s_tracker.RecordSiteId(profile.SiteId);
        RegisterValidatorOnce(services);

        return services;
    }

    /// <summary>
    /// Register a site profile from an existing instance.
    /// Useful when profile needs constructor injection or runtime configuration.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
    public static IServiceCollection AddSiteProfile(
        this IServiceCollection services,
        ISiteProfile profile,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(profile);
        services.AddSingleton<ISiteProfile>(profile);
        profile.RegisterServices(services, configuration);

        // Track site ID for startup validation
        s_tracker.RecordSiteId(profile.SiteId);
        RegisterValidatorOnce(services);

        return services;
    }

    /// <summary>
    /// Register ALL site profiles from assemblies + per-request resolution.
    /// Use when 1 binary = N sites (shared binary, switch per request).
    ///
    /// Scans assemblies for ISiteProfile implementations, calls RegisterServices on each,
    /// then registers ISiteProfileResolver as scoped — resolves the correct profile per-request
    /// based on siteCodeAccessor (e.g., WorkContext.SiteCode).
    ///
    /// If one site's RegisterServices throws, registration continues for remaining sites.
    /// All failures are collected and thrown as an AggregateException at the end.
    /// Individual failures are also tracked in SiteProfileRegistrationTracker for post-DI reporting.
    ///
    /// Usage:
    /// <code>
    /// services.AddMultiSiteProfiles(
    ///     configuration,
    ///     siteCodeAccessor: sp => sp.GetRequiredService&lt;IWorkContextAccessor&gt;().WorkContext?.SiteCode,
    ///     typeof(TciSiteProfile).Assembly
    /// );
    /// </code>
    /// </summary>
    public static IServiceCollection AddMultiSiteProfiles(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, string?> siteCodeAccessor,
        IMLog? diagnosticLog = null,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(siteCodeAccessor);

        var profiles = new Dictionary<string, ISiteProfile>(StringComparer.OrdinalIgnoreCase);

        // Scan assemblies for ISiteProfile implementations
        // Capture errors per site; continue registering remaining sites even if one fails.
        var errors = new List<(string siteId, Exception ex)>();

        foreach (var assembly in assemblies)
        {
            var profileTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                    && typeof(ISiteProfile).IsAssignableFrom(t)
                    && t.GetConstructor(Type.EmptyTypes) is not null);

            foreach (var type in profileTypes)
            {
                var profile = (ISiteProfile)Activator.CreateInstance(type)!;
                profiles[profile.SiteId] = profile;

                // Each profile registers its own services (DbContext, services, strategies)
                try
                {
                    profile.RegisterServices(services, configuration);
                }
                catch (Exception ex)
                {
                    // Log individual failure via IMLog if available (ecosystem convention per CLAUDE.md)
                    diagnosticLog?.Warn(
                        "[SiteProfile] Site '{SiteId}' failed to register services: {Message}",
                        profile.SiteId, ex.Message);

                    // Record in tracker so SiteProfileStartupValidator can report all failures
                    // after DI is built and IMLog is available
                    s_tracker.RecordRegistrationFailure(profile.SiteId, ex);
                    errors.Add((profile.SiteId, ex));
                }

                // Track site ID for startup validation (even failed sites are tracked)
                s_tracker.RecordSiteId(profile.SiteId);
            }
        }

        // Register all profiles as singletons (for diagnostics / enumeration)
        foreach (var profile in profiles.Values)
        {
            services.AddSingleton<ISiteProfile>(profile);
        }

        // Register ISiteProfileResolver — per-request, resolves correct profile by SiteCode
        services.AddScoped<ISiteProfileResolver>(sp =>
        {
            var siteCode = siteCodeAccessor(sp) ?? "default";
            if (profiles.TryGetValue(siteCode, out var match))
                return new SiteProfileResolver(match);

            // Fallback: try "default" profile
            if (profiles.TryGetValue("default", out var fallback))
                return new SiteProfileResolver(fallback);

            throw new InvalidOperationException(
                $"No ISiteProfile registered for site '{siteCode}'. " +
                $"Available: [{string.Join(", ", profiles.Keys)}]");
        });

        // Register startup validator (once)
        RegisterValidatorOnce(services);

        // Throw aggregate after all successful profiles are registered — partial registration is preserved
        if (errors.Count > 0)
        {
            throw new AggregateException(
                $"SiteProfile registration failed for {errors.Count} site(s): {string.Join(", ", errors.Select(e => e.siteId))}",
                errors.Select(e => e.ex));
        }

        return services;
    }

    /// <summary>
    /// Register a per-request resolved service interface.
    /// Each ISiteProfile registers its implementation as a keyed service (key = SiteId).
    /// This method adds a scoped factory that resolves the correct implementation
    /// based on the current site (via ISiteProfileResolver).
    ///
    /// Usage in Program.cs (after AddMultiSiteProfiles):
    /// <code>
    /// services.AddSiteResolvedService&lt;IUpdateOrderService&gt;();
    /// services.AddSiteResolvedService&lt;IOperMethodStrategy&gt;();
    /// </code>
    ///
    /// Each ISiteProfile.RegisterServices() registers:
    /// <code>
    /// services.AddKeyedScoped&lt;IUpdateOrderService, TciUpdateOrderService&gt;("TCI");
    /// </code>
    /// </summary>
    public static IServiceCollection AddSiteResolvedService<TService>(
        this IServiceCollection services)
        where TService : class
    {
        // Track service type for startup validation
        s_tracker.RecordResolvedServiceType(typeof(TService));

        services.AddScoped<TService>(sp =>
        {
            var resolver = sp.GetRequiredService<ISiteProfileResolver>();
            var siteId = resolver.Current.SiteId;

            // Try exact site key first
            var service = sp.GetKeyedService<TService>(siteId);
            if (service is not null) return service;

            // Fallback to "default" key
            service = sp.GetKeyedService<TService>("default");
            if (service is not null) return service;

            throw new InvalidOperationException(
                $"No keyed service '{typeof(TService).Name}' registered for site '{siteId}' or 'default'. " +
                $"Ensure ISiteProfile.RegisterServices() calls: " +
                $"services.AddKeyedScoped<{typeof(TService).Name}, TImpl>(\"{siteId}\")");
        });

        return services;
    }

    /// <summary>
    /// Disables SiteProfile startup validation. Use for testing or dev-only scenarios per D-04.
    /// Must be called AFTER AddMultiSiteProfiles or AddSiteProfile.
    /// </summary>
    public static IServiceCollection SkipSiteProfileStartupValidation(this IServiceCollection services)
    {
        s_tracker.SetSkipValidation();
        return services;
    }

    // Registers tracker + hosted service exactly once (idempotent based on DI descriptor presence).
    private static void RegisterValidatorOnce(IServiceCollection services)
    {
        if (services.Any(sd => sd.ServiceType == typeof(SiteProfileRegistrationTracker)))
            return;

        services.AddSingleton(s_tracker);
        services.AddHostedService<SiteProfileStartupValidator>();
    }
}
