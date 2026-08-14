namespace Muonroi.SignalR.SignalR;

/// <summary>
/// Broadcasts UI engine schema change events through SignalR.
/// </summary>
/// <param name="serviceProvider">Service provider for resolving hub context.</param>
/// <param name="logger">Logger for diagnostics.</param>
public sealed class MUiEngineSchemaNotifier(
    IServiceProvider serviceProvider,
    IMLog<MUiEngineSchemaNotifier> logger) : IUiEngineSchemaNotifier
{
    /// <summary>
    /// Notifies clients that a schema change occurred.
    /// </summary>
    /// <param name="schemaVersion">Schema version payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task NotifySchemaChangedAsync(
        MUiEngineSchemaVersion schemaVersion,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(schemaVersion);

        IHubContext<MUiEngineHub>? hubContext = serviceProvider.GetService<IHubContext<MUiEngineHub>>();
        if (hubContext is null)
        {
            logger.LogDebug("SignalR hub context is not available. Skipped schema change broadcast.");
            return;
        }

        await hubContext.Clients
            .Group(MUiEngineHub.MSchemaWatcherGroup)
            .SendAsync("SchemaChanged", schemaVersion.SchemaHash, schemaVersion, cancellationToken);
    }
}
