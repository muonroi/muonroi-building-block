using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.SignalR.Services;

/// <summary>
/// SignalR hub for UI engine schema notifications.
/// </summary>
public sealed class MUiEngineHub : Hub
{
    /// <summary>
    /// Group name for schema watchers.
    /// </summary>
    public const string MSchemaWatcherGroup = "mui-engine-schema-watchers";

    /// <summary>
    /// Subscribes the current connection to schema change notifications.
    /// </summary>
    /// <returns>A task that completes when the subscription is added.</returns>
    public Task SubscribeToSchemaChanges()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, MSchemaWatcherGroup);
    }

    /// <summary>
    /// Unsubscribes the current connection from schema change notifications.
    /// </summary>
    /// <returns>A task that completes when the subscription is removed.</returns>
    public Task UnsubscribeFromSchemaChanges()
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, MSchemaWatcherGroup);
    }
}

/// <summary>
/// Broadcasts UI engine schema change events through SignalR.
/// </summary>
/// <param name="hubContext">SignalR hub context.</param>
public sealed class MUiEngineSchemaNotifier(
    IHubContext<MUiEngineHub> hubContext) : Muonroi.Core.Abstractions.Interfaces.IUiEngineSchemaNotifier
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

        await hubContext.Clients
            .Group(MUiEngineHub.MSchemaWatcherGroup)
            .SendAsync("SchemaChanged", schemaVersion.SchemaHash, schemaVersion, cancellationToken);
    }
}
