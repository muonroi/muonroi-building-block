namespace Muonroi.SignalR.SignalR;

public sealed class MUiEngineSchemaNotifier(
    IServiceProvider serviceProvider,
    ILogger<MUiEngineSchemaNotifier> logger) : IUiEngineSchemaNotifier
{
    public async Task NotifySchemaChangedAsync(
        MUiEngineSchemaVersion schemaVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schemaVersion);

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
