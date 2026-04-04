namespace Muonroi.RuleEngine.CEP.Abstractions;

/// <summary>
/// Provides persistence for CEP window configurations.
/// </summary>
public interface ICepConfigRepository
{
    /// <summary>
    /// Lists configurations visible in the current execution context.
    /// </summary>
    Task<IReadOnlyList<CepConfig>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a configuration by identifier in the current execution context.
    /// </summary>
    Task<CepConfig?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a configuration.
    /// </summary>
    Task<CepConfig> SaveAsync(CepConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a configuration if present.
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
