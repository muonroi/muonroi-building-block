namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Represents the IOutbox Relay Service.
/// </summary>
public interface IOutboxRelayService
{
    /// <summary>
    /// Executes the Relay Pending Async operation.
    /// </summary>
    Task RelayPendingAsync(CancellationToken cancellationToken = default);
}
