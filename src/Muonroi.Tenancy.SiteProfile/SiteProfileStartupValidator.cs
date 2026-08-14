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
[SuppressMessage("Muonroi.CodeStandards", "MSTD0003",
    Justification = "Hosted service registered by AddSiteProfile. Tenancy.SiteProfile does not register Muonroi.Logging, and consumers (incl. minimal/standalone hosts) may not call AddMuonroiLogging, so IMLog<T> is not guaranteed in this DI scope — requiring it breaks container ValidateOnBuild. Logs via the always-available Microsoft ILogger.")]
internal sealed class SiteProfileStartupValidator(
    SiteProfileRegistrationTracker tracker,
    IServiceProvider serviceProvider,
    ILogger<SiteProfileStartupValidator> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Validate SiteAssemblies discovery first (non-blocking — warns but does not throw)
        ValidateSiteAssemblyDiscovery();

        if (tracker.SkipValidation)
        {
            logger.LogInformation("SiteProfile startup validation skipped (SkipStartupValidation enabled)");
            // gRPC client validation is independent of SkipValidation — always run it.
            ValidateSiteGrpcClientRegistrations();
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
            MGuard.Fail<object>(message);
        }

        logger.LogInformation(
            "SiteProfile startup validation passed: {SiteCount} site(s) \u00d7 {ServiceCount} service type(s) verified",
            tracker.SiteIds.Count,
            tracker.ResolvedServiceTypes.Count);

        // Validate gRPC client registrations (non-blocking — warns but does not throw)
        ValidateSiteGrpcClientRegistrations();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Validates that all ISiteProfile implementations from <see cref="SiteProfileOptions.SiteAssemblies"/>
    /// were actually discovered during registration. Logs a warning for each undiscovered profile
    /// so operators can detect misconfigured assembly lists without failing startup (per D-15).
    ///
    /// Non-blocking: warnings only. Does not throw.
    /// </summary>
    private void ValidateSiteAssemblyDiscovery()
    {
        var options = serviceProvider.GetService<IOptions<SiteProfileOptions>>()?.Value;

        if (options?.SiteAssemblies is not { Length: > 0 } siteAssemblies)
            return;

        foreach (var assembly in siteAssemblies)
        {
            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ISiteProfile).IsAssignableFrom(t));
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Partial load — use the types that loaded successfully
                types = (ex.Types ?? [])
                    .Where(t => t is not null && !t.IsAbstract && !t.IsInterface && typeof(ISiteProfile).IsAssignableFrom(t))
                    .Cast<Type>();
            }

            foreach (var profileType in types)
            {
                ISiteProfile? instance;
                try
                {
                    instance = Activator.CreateInstance(profileType) as ISiteProfile;
                }
                catch
                {
                    // Cannot instantiate to get SiteId — skip discovery check for this profile
                    continue;
                }

                if (instance is null) continue;

                var siteId = instance.SiteId;

                if (!tracker.SiteIds.Contains(siteId))
                {
                    logger.LogWarning(
                        "[SITE-ASSEMBLY] ISiteProfile '{ProfileType}' in assembly '{Assembly}' with SiteId '{SiteId}' " +
                        "was not discovered during registration. Ensure AddMultiSiteProfiles includes this assembly.",
                        profileType.Name,
                        assembly.GetName().Name,
                        siteId);
                }
            }
        }
    }

    /// <summary>
    /// Validates that every site gRPC service entry has a matching keyed client registration.
    /// Discovers the registry type via AppDomain reflection — no-op when no registry type is found
    /// (i.e., when the project does not use gRPC, per D-14).
    ///
    /// Logs a warning for each missing client registration (per D-13 — warning, not exception).
    /// </summary>
    private void ValidateSiteGrpcClientRegistrations()
    {
        // Discover registry type: look for any type whose name contains "SiteGrpcServiceRegistry"
        // (production: SiteGrpcServiceRegistry, tests: SiteGrpcServiceRegistryStub).
        // This is an AppDomain scan — runs once at startup, not on the hot path.
        Type? registryType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return []; }
            })
            .FirstOrDefault(t => t.Name.Contains("SiteGrpcServiceRegistry", StringComparison.Ordinal)
                && !t.IsAbstract
                && !t.IsInterface);

        if (registryType is null)
        {
            // No registry type found — project does not use gRPC. No-op per D-14.
            return;
        }

        // Try to resolve an instance from the DI container first (test scenario: stub registered in DI).
        // Fall back to creating a new instance via reflection if not in DI.
        object? registryInstance = serviceProvider.GetService(registryType);
        if (registryInstance is null)
        {
            try
            {
                registryInstance = Activator.CreateInstance(registryType);
            }
            catch
            {
                // Cannot instantiate registry — skip gRPC validation
                return;
            }
        }

        // Discover the method to get all entries.
        // Production: GetAllSiteGrpcServices() on static class — try as instance method first, then static.
        // Test stub: GetAllEntries() on instance.
        MethodInfo? getAllMethod = registryType.GetMethod("GetAllSiteGrpcServices",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            ?? registryType.GetMethod("GetAllEntries",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        if (getAllMethod is null)
        {
            // Registry type found but no known method — skip
            return;
        }

        IEnumerable? entries;
        try
        {
            entries = getAllMethod.IsStatic
                ? getAllMethod.Invoke(null, null) as IEnumerable
                : getAllMethod.Invoke(registryInstance, null) as IEnumerable;
        }
        catch
        {
            return;
        }

        if (entries is null)
            return;

        if (serviceProvider is not IKeyedServiceProvider keyedProvider)
            return;

        foreach (var entry in entries)
        {
            if (entry is null) continue;

            var entryType = entry.GetType();

            // Read SiteId and ClientType via reflection (entry is either SiteGrpcServiceEntry or SiteGrpcEntryStub)
            var siteIdProp = entryType.GetProperty("SiteId");
            var clientTypeProp = entryType.GetProperty("ClientType");

            if (siteIdProp is null || clientTypeProp is null) continue;

            var siteId = siteIdProp.GetValue(entry) as string;
            var clientType = clientTypeProp.GetValue(entry) as Type;

            if (string.IsNullOrWhiteSpace(siteId) || clientType is null) continue;

            var resolved = keyedProvider.GetKeyedService(clientType, siteId);
            if (resolved is null)
            {
                logger.LogWarning(
                    "[SITE-GRPC] Site '{SiteId}' has no gRPC client registration for '{ClientType}'. " +
                    "Verify AddSiteGrpcClient<{ClientType}>(\"{SiteId}\") was called.",
                    siteId, clientType.Name, clientType.Name, siteId);
            }
        }
    }
}
