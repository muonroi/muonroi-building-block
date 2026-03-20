namespace Muonroi.SignalR.SignalR;

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
