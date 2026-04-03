namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Contributes to the UI Engine manifest.
/// </summary>
public interface IUiEngineManifestContributor
{
    /// <summary>
    /// Gets the order in which this contributor should be executed.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gets the unique identifier of the module.
    /// </summary>
    string ModuleId { get; }

    /// <summary>
    /// Gets the required tier for this contributor.
    /// </summary>
    string RequiredTier { get; }

    /// <summary>
    /// Contributes to the UI Engine manifest asynchronously.
    /// </summary>
    /// <param name="context">The manifest context.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default);
}
