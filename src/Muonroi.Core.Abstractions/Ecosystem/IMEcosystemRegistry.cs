namespace Muonroi.Core.Abstractions.Ecosystem;

/// <summary>
/// Registry that tracks which ecosystem capabilities are active in the current application.
/// Registered as a singleton; each package registers its capability during DI setup.
/// </summary>
public interface IMEcosystemRegistry
{
    /// <summary>
    /// Marks one or more capabilities as active. Idempotent — registering the same
    /// capability multiple times has no additional effect.
    /// </summary>
    /// <param name="capability">One or more <see cref="MCapability"/> flags to register.</param>
    void Register(MCapability capability);

    /// <summary>
    /// Returns <c>true</c> when ALL flags in <paramref name="capability"/> are currently active.
    /// </summary>
    /// <param name="capability">One or more <see cref="MCapability"/> flags to test.</param>
    bool Has(MCapability capability);

    /// <summary>
    /// Gets the combined set of all registered capabilities at the current moment.
    /// </summary>
    MCapability Active { get; }
}
