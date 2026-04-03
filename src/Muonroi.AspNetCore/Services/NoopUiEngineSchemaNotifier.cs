namespace Muonroi.AspNetCore.Services;

internal sealed class NoopUiEngineSchemaNotifier : IUiEngineSchemaNotifier
{
    public Task NotifySchemaChangedAsync(MUiEngineSchemaVersion schemaVersion, CancellationToken cancellationToken = default)
    {
        _ = schemaVersion;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
