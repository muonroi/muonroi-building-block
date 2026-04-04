using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Muonroi.Core.Abstractions.Ecosystem;

/// <summary>
/// Logs the active ecosystem capabilities at application startup.
/// Automatically registered by <see cref="EcosystemServiceCollectionExtensions.GetOrCreateRegistry"/>.
/// </summary>
internal sealed class MEcosystemStartupFilter : IStartupFilter
{
    private readonly IMEcosystemRegistry _registry;
    private readonly ILogger<MEcosystemStartupFilter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MEcosystemStartupFilter"/>.
    /// </summary>
    public MEcosystemStartupFilter(IMEcosystemRegistry registry, ILogger<MEcosystemStartupFilter> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc />
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
