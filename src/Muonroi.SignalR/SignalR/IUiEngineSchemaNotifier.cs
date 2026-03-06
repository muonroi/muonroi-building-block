namespace Muonroi.SignalR.SignalR;

public interface IUiEngineSchemaNotifier
{
    Task NotifySchemaChangedAsync(MUiEngineSchemaVersion schemaVersion, CancellationToken cancellationToken = default);
}
