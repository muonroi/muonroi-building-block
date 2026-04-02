using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Validates at startup that every registered ISiteProfile.SiteId has matching keyed service
/// registrations for every AddSiteResolvedService&lt;T&gt;() call.
///
/// Throws <see cref="InvalidOperationException"/> listing ALL missing site × service pairs
/// so operators can fix all gaps in one restart cycle (fail-fast per D-01).
///
/// Opt-out: call services.SkipSiteProfileStartupValidation() for test/dev scenarios per D-04.
/// </summary>
internal sealed class SiteProfileStartupValidator(
    SiteProfileRegistrationTracker tracker,
    IServiceProvider serviceProvider,
    ILogger<SiteProfileStartupValidator> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (tracker.SkipValidation)
        {
            logger.LogInformation("SiteProfile startup validation skipped (SkipStartupValidation enabled)");
            return Task.CompletedTask;
        }

        var missing = new List<string>();

        if (serviceProvider is IKeyedServiceProvider keyedProvider)
        {
            foreach (var serviceType in tracker.ResolvedServiceTypes)
            {
                if (serviceType == null) continue;

                foreach (var siteId in tracker.SiteIds)
                {
                    if (string.IsNullOrWhiteSpace(siteId)) continue;

                    var resolved = keyedProvider.GetKeyedService(serviceType, siteId);
                    if (resolved is null)
                    {
                        // Check "default" fallback before reporting as missing
                        var fallback = keyedProvider.GetKeyedService(serviceType, "default");
                        if (fallback is not null)
                        {
                            // Site-specific missing but "default" exists — warn
                            logger.LogWarning(
                                "[SITE-SAFETY] Site '{SiteId}' has no keyed registration for '{ServiceType}', " +
                                "using 'default' fallback. Register a site-specific service to suppress this warning.",
                                siteId, serviceType.Name);

                            // In strict mode, treat fallback as error
                            var options = serviceProvider.GetService<IOptions<SiteProfileOptions>>()?.Value;
                            if (options?.StrictMode == true)
                            {
                                missing.Add(
                                    $"  - Site '{siteId}' x Service '{serviceType.Name}': " +
                                    $"no site-specific registration (StrictMode rejects 'default' fallback)");
                            }
                        }
                        else
                        {
                            missing.Add(
                                $"  - Site '{siteId}' x Service '{serviceType.Name}': " +
                                $"no keyed registration for key \"{siteId}\" or \"default\"");
                        }
                    }
                }
            }
        }

        if (missing.Count > 0)
        {
            var message =
                $"SiteProfile startup validation FAILED \u2014 {missing.Count} missing keyed service registration(s):\n" +
                $"{string.Join("\n", missing)}\n\n" +
                $"Ensure each ISiteProfile.RegisterServices() calls " +
                $"services.AddKeyed*<TService, TImpl>(siteId) for every " +
                $"AddSiteResolvedService<TService>() in Program.cs.";
            throw new InvalidOperationException(message);
        }

        logger.LogInformation(
            "SiteProfile startup validation passed: {SiteCount} site(s) \u00d7 {ServiceCount} service type(s) verified",
            tracker.SiteIds.Count,
            tracker.ResolvedServiceTypes.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
