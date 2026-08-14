namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Abstraction for the inbox pattern store to decouple from specific DB implementations.
/// </summary>
public interface IMessageInboxStore
{
    /// <summary>
    /// Checks if a message has already been processed by a consumer.
    /// </summary>
    Task<bool> HasBeenProcessedAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a message has been processed by a consumer.
    /// </summary>
    /// <remarks>
    /// This should ideally participate in the ambient transaction of the consumer.
    /// </remarks>
    Task RecordProcessedAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default);
}
