namespace Muonroi.Core.Abstractions.Ecosystem;

/// <summary>
/// Logs the active ecosystem capabilities at application startup.
/// Automatically registered by <see cref="EcosystemServiceCollectionExtensions.GetOrCreateRegistry"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="MEcosystemStartupFilter"/>.
/// </remarks>
internal sealed class MEcosystemStartupFilter(IMEcosystemRegistry registry, ILogger<MEcosystemStartupFilter> logger) : IStartupFilter
{
    private readonly IMEcosystemRegistry _registry = registry;
    private readonly ILogger<MEcosystemStartupFilter> _logger = logger;

    /// <inheritdoc />
    [SuppressMessage("Muonroi.CodeStandards", "MSTD0003",
        Justification = "Foundational startup filter registered by the ecosystem core (Muonroi.Core.Abstractions). It uses the always-available Microsoft ILogger; migrating to IMLog would make the optional Muonroi.Logging package a hard dependency of the ecosystem registry and risk a DI resolution failure at startup for consumers that do not call AddMuonroiLogging.")]
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        MCapability active = _registry.Active;
        string names = active == MCapability.None
            ? "None"
            : string.Join(", ", Enum.GetValues<MCapability>()
                .Where(c => c != MCapability.None && _registry.Has(c))
                .Select(c => c.ToString()));

        _logger.LogInformation("[Muonroi.Ecosystem] Active: {Capabilities}", names);

        return next;
    }
}
