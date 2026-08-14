namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Extension methods for ISiteProfile registration.
/// </summary>
public static class SiteProfileExtensions
{
    // Static tracker — populated during DI setup (single-threaded), consumed read-only at startup.
    private static readonly SiteProfileRegistrationTracker s_tracker = new();

    /// <summary>
    /// Reset the tracker for testing.
    /// </summary>
    internal static void ResetTracker() => s_tracker.Clear();

    /// <summary>
    /// Shared tracker instance — accessed by SiteProfileConventionExtensions for convention registration tracking.
    /// </summary>
    internal static SiteProfileRegistrationTracker SharedTracker => s_tracker;

    /// <summary>
    /// Configures SiteProfile options including strict mode for site resolution.
    /// </summary>
    public static IServiceCollection ConfigureSiteProfile(this IServiceCollection services, Action<SiteProfileOptions> configure)
    {
        services.Configure(configure);
        return services;
    }

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
        MGuard.NotNull(profile);
        services.AddSingleton(profile);
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
    /// Scans assemblies for ISiteProfile implementations via reflection,
    /// then delegates to the core registration method.
    /// </summary>
    public static IServiceCollection AddMultiSiteProfiles(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, string?> siteCodeAccessor,
        IMLog? diagnosticLog = null,
        params Assembly[] assemblies)
    {
        MGuard.NotNull(siteCodeAccessor);

        // Scan assemblies for ISiteProfile implementations (reflection-based discovery)
        var discoveredProfiles = new List<ISiteProfile>();
        foreach (var assembly in assemblies)
        {
            var profileTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                    && typeof(ISiteProfile).IsAssignableFrom(t)
                    && t.GetConstructor(Type.EmptyTypes) is not null);

            foreach (var type in profileTypes)
            {
                discoveredProfiles.Add((ISiteProfile)MGuard.NotNull(Activator.CreateInstance(type)));
            }
        }

        return AddMultiSiteProfilesCore(services, configuration, siteCodeAccessor,
            [.. discoveredProfiles], diagnosticLog);
    }

    /// <summary>
    /// Register ALL site profiles from an explicit array + per-request resolution.
    /// AOT-safe overload — profiles are pre-instantiated (no reflection).
    /// Called by generated <c>AddGeneratedSiteProfiles()</c> with manifest array.
    ///
    /// <code>
    /// // Generated code calls:
    /// SiteProfileExtensions.AddMultiSiteProfilesCore(
    ///     services, configuration, siteCodeAccessor, SiteProfileManifest.CreateAll(), logFactory);
    /// </code>
    /// </summary>
    public static IServiceCollection AddMultiSiteProfilesCore(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, string?> siteCodeAccessor,
        ISiteProfile[] profiles,
        IMLog? diagnosticLog = null)
    {
        MGuard.NotNull(siteCodeAccessor);

        services.Configure<SiteProfileOptions>(_ => { }); // Register with defaults if not already configured

        var profileMap = new Dictionary<string, ISiteProfile>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<(string siteId, Exception ex)>();

        // Set ambient logger for SiteProfileBootstrap (called inside RegisterServices).
        // Avoids BuildServiceProvider() anti-pattern — DI registration is single-threaded.
        var previousLog = SiteProfileBootstrap.CurrentLog;
        SiteProfileBootstrap.CurrentLog = diagnosticLog;
        try
        {
            foreach (var profile in profiles)
            {
                profileMap[profile.SiteId] = profile;

                try
                {
                    profile.RegisterServices(services, configuration);
                }
                catch (Exception ex)
                {
                    diagnosticLog?.Warn(
                        "[SiteProfile] Site '{SiteId}' failed to register services: {Message}",
                        profile.SiteId, ex.Message);
                    s_tracker.RecordRegistrationFailure(profile.SiteId, ex);
                    errors.Add((profile.SiteId, ex));
                }

                s_tracker.RecordSiteId(profile.SiteId);
            }
        }
        finally
        {
            SiteProfileBootstrap.CurrentLog = previousLog;
        }

        // Register all profiles as singletons (for diagnostics / enumeration)
        foreach (var profile in profileMap.Values)
        {
            services.AddSingleton(profile);
        }

        // Register ISiteProfileResolver — per-request, resolves correct profile by SiteCode
        services.AddScoped<ISiteProfileResolver>(sp =>
        {
            // SiteProfileScope takes precedence — used in tests and background jobs
            var scopeOverride = SiteProfileScope.Current;
            if (scopeOverride is not null)
                return new SiteProfileResolver(scopeOverride);

            var siteCode = siteCodeAccessor(sp) ?? "default";
            if (profileMap.TryGetValue(siteCode, out var match))
                return new SiteProfileResolver(match);

            // Site code not found — check strict mode
            var options = sp.GetService<IOptions<SiteProfileOptions>>()?.Value;
            if (options?.StrictMode == true)
            {
                return MGuard.Fail<ISiteProfileResolver>(
                    $"[SITE-SAFETY] No ISiteProfile registered for site '{siteCode}'. " +
                    $"StrictMode is enabled — no fallback to 'default'. " +
                    $"Available: [{string.Join(", ", profileMap.Keys)}]");
            }

            // Non-strict: fallback to "default" with warning
            if (profileMap.TryGetValue("default", out var fallback))
            {
                var logger = sp.GetService<IMLog<SiteProfileResolver>>();
                logger?.Warn(
                    "[SITE-SAFETY] Site '{SiteCode}' not found, falling back to 'default' profile. " +
                    "Enable SiteProfileOptions.StrictMode to make this an error.",
                    siteCode);
                return new SiteProfileResolver(fallback);
            }

            return MGuard.Fail<ISiteProfileResolver>(
                $"No ISiteProfile registered for site '{siteCode}'. " +
                $"Available: [{string.Join(", ", profileMap.Keys)}]");
        });

        // Register startup validator (once)
        RegisterValidatorOnce(services);

        // Throw aggregate after all successful profiles are registered — partial registration is preserved
        if (errors.Count > 0)
        {
#pragma warning disable MSTD0001 // AggregateException preserves per-site inner exceptions
            throw new AggregateException(
                $"SiteProfile registration failed for {errors.Count} site(s): {string.Join(", ", errors.Select(e => e.siteId))}",
                errors.Select(e => e.ex));
#pragma warning restore MSTD0001
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

        services.AddScoped(sp =>
        {
            string siteId = SiteProfileScope.Current?.SiteId
                ?? sp.GetRequiredService<ISiteProfileResolver>().Current.SiteId;

            // Try exact site key first
            var service = sp.GetKeyedService<TService>(siteId);
            if (service is not null) return service;

            // Fallback to "default" key
            service = sp.GetKeyedService<TService>("default");
            if (service is not null) return service;

            return MGuard.Fail<TService>(
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
