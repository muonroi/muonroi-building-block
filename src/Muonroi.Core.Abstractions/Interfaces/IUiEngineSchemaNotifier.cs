namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Notifies when the UI Engine schema has changed.
/// </summary>
public interface IUiEngineSchemaNotifier
{
    /// <summary>
    /// Notifies that the UI Engine schema has changed asynchronously.
    /// </summary>
    /// <param name="schemaVersion">The new schema version information.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task NotifySchemaChangedAsync(MUiEngineSchemaVersion schemaVersion, CancellationToken cancellationToken = default);
}
