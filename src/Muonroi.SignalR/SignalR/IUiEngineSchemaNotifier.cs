namespace Muonroi.SignalR.SignalR;

/// <summary>
/// Notifies clients about UI engine schema changes.
/// </summary>
public interface IUiEngineSchemaNotifier
{
    /// <summary>
    /// Sends a schema change notification.
    /// </summary>
    /// <param name="schemaVersion">Schema version payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifySchemaChangedAsync(MUiEngineSchemaVersion schemaVersion, CancellationToken cancellationToken = default);
}
