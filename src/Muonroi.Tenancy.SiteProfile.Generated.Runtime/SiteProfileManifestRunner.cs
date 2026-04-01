using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Tenancy.SiteProfile.Generated.Runtime;

/// <summary>
/// Runtime execution logic for AOT source-generated SiteProfile manifests.
/// Called by generated code — contains all DI registration, resolver wiring,
/// and logging logic that was previously emitted inline by the source generator.
///
/// This class exists in regular C# (not generated StringBuilder code) so it gets
/// full IDE support, is testable, and maintainable.
/// </summary>
public static class SiteProfileManifestRunner
{
    /// <summary>
    /// Registers all site profiles, wires ISiteProfileResolver (scoped, per-request),
    /// and registers each profile as an ISiteProfile singleton.
    /// </summary>
    /// <param name="profiles">AOT-safe manifest array — each element created via new() in generated code.</param>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="siteCodeAccessor">Per-request site code resolution delegate.</param>
    /// <param name="logFactory">Optional IMLogFactory for structured logging.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Register(
        ISiteProfile[] profiles,
        IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, string?> siteCodeAccessor,
        IMLogFactory? logFactory = null)
    {
        ArgumentNullException.ThrowIfNull(siteCodeAccessor);

        var log = logFactory?.CreateLogger("Muonroi.SiteProfile.AOT");
        var profileMap = new Dictionary<string, ISiteProfile>(StringComparer.OrdinalIgnoreCase);

        log?.Info("[SiteProfile-AOT] AddGeneratedSiteProfiles — begin registration");

        // Register each profile — explicit instantiation happened in generated manifest (AOT-safe)
        foreach (var profile in profiles)
        {
            profileMap[profile.SiteId] = profile;
            log?.Info("[SiteProfile-AOT] Instantiated {SiteId} ({ProfileType})", profile.SiteId, profile.GetType().Name);
            profile.RegisterServices(services, configuration);
            log?.Info("[SiteProfile-AOT] Registered services for {SiteId}", profile.SiteId);
        }

        // Register all profiles as singletons (for diagnostics / enumeration)
        foreach (var p in profileMap.Values)
            services.AddSingleton<ISiteProfile>(p);

        log?.Info("[SiteProfile-AOT] Registered {Count} profile(s) as singletons: [{SiteIds}]",
            profileMap.Count, string.Join(", ", profileMap.Keys));

        // Register ISiteProfileResolver — per-request, resolves correct profile by SiteCode
        services.AddScoped<ISiteProfileResolver>(sp =>
        {
            // SiteProfileScope takes precedence — used in tests and background jobs
            var scopeOverride = SiteProfileScope.Current;
            if (scopeOverride is not null)
                return new SiteProfileResolver(scopeOverride);

            var reqLog = sp.GetService<IMLogFactory>()?.CreateLogger("Muonroi.SiteProfile.AOT.Resolver");
            var siteCode = siteCodeAccessor(sp) ?? "default";

            if (profileMap.TryGetValue(siteCode, out var match))
            {
                reqLog?.Debug("[SiteProfile-AOT] Resolved site '{SiteCode}' -> {ResolvedSiteId}", siteCode, match.SiteId);
                return new SiteProfileResolver(match);
            }

            // Site code not found — check strict mode
            var options = sp.GetService<IOptions<SiteProfileOptions>>()?.Value;
            if (options?.StrictMode == true)
            {
                throw new InvalidOperationException(
                    $"[SITE-SAFETY] No ISiteProfile registered for site '{siteCode}'. " +
                    $"StrictMode is enabled — no fallback to 'default'. " +
                    $"Available: [{string.Join(", ", profileMap.Keys)}]");
            }

            // Non-strict: fallback to "default" with warning
            if (profileMap.TryGetValue("default", out var fallback))
            {
                reqLog?.Warn("[SiteProfile-AOT] Site '{SiteCode}' not found, falling back to 'default'", siteCode);
                return new SiteProfileResolver(fallback);
            }

            reqLog?.Error(null, "[SiteProfile-AOT] No ISiteProfile for site '{SiteCode}'. Available: [{AvailableSites}]",
                siteCode, string.Join(", ", profileMap.Keys));
            throw new InvalidOperationException(
                $"No ISiteProfile for site '{siteCode}'. " +
                $"Available: [{string.Join(", ", profileMap.Keys)}]");
        });

        log?.Info("[SiteProfile-AOT] AddGeneratedSiteProfiles — registration complete");
        return services;
    }
}
