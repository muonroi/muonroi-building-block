namespace Muonroi.Core.Abstractions.Interfaces;

public interface IUiEngineSchemaNotifier
{
    Task NotifySchemaChangedAsync(MUiEngineSchemaVersion schemaVersion, CancellationToken cancellationToken = default);
}
