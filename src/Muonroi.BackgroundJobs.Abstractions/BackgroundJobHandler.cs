namespace Muonroi.BackgroundJobs.Abstractions;

/// <summary>
/// Delegate type for provider-specific DI registration.
/// Called by provider packages when they are selected as the active job backend.
/// </summary>
/// <param name="services">The service collection to configure.</param>
/// <param name="configuration">The application configuration.</param>
/// <returns>The configured service collection.</returns>
public delegate IServiceCollection JobProviderRegistration(IServiceCollection services, IConfiguration configuration);

/// <summary>
/// Resolves and registers the configured background job provider.
/// Uses a compile-time delegate dictionary — no reflection, AOT-safe (SEC-02).
/// </summary>
public static class BackgroundJobHandler
{
    /// <summary>
    /// Compile-time provider registration dictionary.
    /// Provider packages self-register via <see cref="RegisterProvider"/> at module load time.
    /// </summary>
    private static readonly Dictionary<JobType, JobProviderRegistration> s_providers = [];

    /// <summary>
    /// Registers a job provider for the given <paramref name="jobType"/>.
    /// Provider packages must call this in a module initializer or static constructor
    /// before <see cref="AddBackgroundJobs"/> is invoked.
    /// </summary>
    /// <param name="jobType">The job type this provider handles.</param>
    /// <param name="registration">The compile-time delegate to invoke during DI setup.</param>
    public static void RegisterProvider(JobType jobType, JobProviderRegistration registration)
    {
        s_providers[jobType] = registration;
    }

    /// <summary>
    /// Adds background job services based on configuration.
    /// Dispatches to the registered provider for the configured <see cref="JobType"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="MConfigurationException">
    /// Thrown when no provider is registered for the configured <see cref="JobType"/>.
    /// </exception>
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        BackgroundJobConfigs cfg = new();
        configuration.GetSection(BackgroundJobConfigs.SectionName).Bind(cfg);

        if (!s_providers.TryGetValue(cfg.JobType, out JobProviderRegistration? registration))
        {
            MGuard.Configured(false, 
                $"No background job provider registered for type '{cfg.JobType}'. " +
                "Ensure the provider package (e.g., Muonroi.BackgroundJobs.Hangfire) is referenced and loaded.",
                "BackgroundJobs:JobType");
        }

        // +Logging: when Logging capability is active, log the selected provider at registration time.
        // Resolves the registry from the service collection if already registered.
        IMEcosystemRegistry? registry = services
            .FirstOrDefault(sd => sd.ServiceType == typeof(IMEcosystemRegistry))
            ?.ImplementationInstance as IMEcosystemRegistry;

        if (registry?.Has(MCapability.Logging) == true)
        {
            // INTENTIONAL DEGRADATION: Console.WriteLine used because ILogger/IMLog is not
            // available at DI registration time (BuildServiceProvider not yet called).
#pragma warning disable MSTD0003
            Console.WriteLine($"[Ecosystem] BackgroundJobs: Dispatching to {cfg.JobType} provider");
#pragma warning restore MSTD0003
        }

        // +RuleEngine: When RuleEngine capability is present, job routing can be
        // extended via rules. See IMEcosystemRegistry.Has(MCapability.RuleEngine).
        // Implementation deferred to a future phase.

        return MGuard.NotNull(registration)(services, configuration);
    }
}
