using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Convention-based site service registration.
/// Reduces per-site boilerplate by auto-discovering <typeparamref name="TServiceInterface"/> implementations
/// and registering them as keyed services using the site ID of the enclosing ISiteProfile (D-06).
/// </summary>
public static class SiteProfileConventionExtensions
{
    /// <summary>
    /// Scans each assembly for ISiteProfile implementations and matching
    /// <typeparamref name="TServiceInterface"/> implementations, then registers each match
    /// as a keyed scoped service (key = SiteId) and wires per-request resolution via
    /// <see cref="SiteProfileExtensions.AddSiteResolvedService{TService}"/>.
    ///
    /// Discovery rules (per D-06 through D-10):
    /// <list type="bullet">
    ///   <item>Assemblies with no ISiteProfile implementations are silently skipped (D-10).</item>
    ///   <item>ISiteProfile must have a parameterless constructor (same constraint as AddMultiSiteProfiles).</item>
    ///   <item>If the assembly has no concrete <typeparamref name="TServiceInterface"/> implementation, the site is silently skipped (D-08).</item>
    ///   <item>If a keyed service for (TServiceInterface, siteId) is already registered, the convention does not overwrite it (D-09).</item>
    ///   <item>When an assembly has multiple ISiteProfile impls, each is paired with a TServiceInterface impl sharing the same namespace prefix (D-07).</item>
    /// </list>
    /// </summary>
    /// <typeparam name="TServiceInterface">The service interface to discover implementations for.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSiteConventionServices<TServiceInterface>(
        this IServiceCollection services,
        params Assembly[] assemblies)
        where TServiceInterface : class
    {
        if (assemblies is null || assemblies.Length == 0)
            return services;

        var tracker = SiteProfileExtensions.SharedTracker;
        var serviceInterface = typeof(TServiceInterface);

        // Track unique assemblies (caller may pass duplicates)
        var seen = new HashSet<Assembly>(ReferenceEqualityComparer.Instance);

        foreach (var assembly in assemblies)
        {
            if (assembly is null || !seen.Add(assembly))
                continue;

            // Find all ISiteProfile implementations in this assembly
            var profileTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                    && typeof(ISiteProfile).IsAssignableFrom(t)
                    && t.GetConstructor(Type.EmptyTypes) is not null)
                .ToList();

            if (profileTypes.Count == 0)
            {
                // No ISiteProfile in this assembly — skip silently (per D-10)
                continue;
            }

            // Find all concrete TServiceInterface implementations in this assembly
            var implTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                    && serviceInterface.IsAssignableFrom(t))
                .ToList();

            if (implTypes.Count == 0)
            {
                // No TServiceInterface impl in this assembly — silently skip all sites (per D-08)
                continue;
            }

            foreach (var profileType in profileTypes)
            {
                // Instantiate ISiteProfile to get SiteId (parameterless ctor guaranteed above)
                ISiteProfile profile;
                try
                {
                    profile = (ISiteProfile)Activator.CreateInstance(profileType)!;
                }
                catch
                {
                    // Profile instantiation failed — skip this profile silently
                    continue;
                }

                var siteId = profile.SiteId;

                // Pair profile with impl: prefer impl whose namespace contains the profile's namespace prefix,
                // falling back to the first impl when no namespace match found (D-07).
                var profileNs = profileType.Namespace ?? string.Empty;
                var matchedImpl = implTypes.FirstOrDefault(t =>
                    t.Namespace is not null && t.Namespace.StartsWith(profileNs, StringComparison.OrdinalIgnoreCase))
                    ?? implTypes[0];

                // Check if already registered for this key (per D-09 — explicit registration wins)
                var alreadyRegistered = services.Any(sd =>
                    sd.ServiceType == serviceInterface
                    && sd.IsKeyedService
                    && sd.ServiceKey is string key
                    && StringComparer.OrdinalIgnoreCase.Equals(key, siteId));

                if (!alreadyRegistered)
                {
                    services.AddKeyedScoped(serviceInterface, siteId, matchedImpl);
                }

                // Track the discovered service type in the shared tracker
                tracker.RecordResolvedServiceType(serviceInterface);
            }
        }

        // Wire per-request resolution (idempotent — SiteProfileExtensions guards duplicates via tracker)
        services.AddSiteResolvedService<TServiceInterface>();

        return services;
    }
}
