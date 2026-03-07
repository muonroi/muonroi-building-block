namespace Muonroi.Messaging.Abstractions.Contracts;

public interface IOutboxRelayService
{
    Task RelayPendingAsync(CancellationToken cancellationToken = default);
}
