namespace Muonroi.SignalR.Services;

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

public sealed class MUiEngineSchemaNotifier(
    IHubContext<MUiEngineHub> hubContext) : Muonroi.Core.Abstractions.Interfaces.IUiEngineSchemaNotifier
{
    public async Task NotifySchemaChangedAsync(
        MUiEngineSchemaVersion schemaVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schemaVersion);

        await hubContext.Clients
            .Group(MUiEngineHub.MSchemaWatcherGroup)
            .SendAsync("SchemaChanged", schemaVersion.SchemaHash, schemaVersion, cancellationToken);
    }
}
