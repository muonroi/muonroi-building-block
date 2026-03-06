namespace Muonroi.SignalR.SignalR;

public sealed class MUiEngineHub : Hub
{
    public const string MSchemaWatcherGroup = "mui-engine-schema-watchers";

    public Task SubscribeToSchemaChanges()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, MSchemaWatcherGroup);
    }

    public Task UnsubscribeFromSchemaChanges()
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, MSchemaWatcherGroup);
    }
}
