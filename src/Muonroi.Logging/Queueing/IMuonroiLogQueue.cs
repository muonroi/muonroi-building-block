namespace Muonroi.Logging.Queueing;

/// <summary>
/// A high-performance, asynchronous log queue that decouples log production from I/O processing.
/// </summary>
public interface IMuonroiLogQueue
{
    /// <summary>
    /// Enqueues a high-priority log event (e.g., Error, Critical, Audit).
    /// Returns false if the queue is full or closed.
    /// </summary>
    bool TryEnqueueHighPriority(LogEvent logEvent);

    /// <summary>
    /// Enqueues a normal-priority log event (e.g., Info, Trace, Debug).
    /// Returns false if the queue is full or closed.
    /// </summary>
    bool TryEnqueueNormal(LogEvent logEvent);

    /// <summary>
    /// Reads high-priority log events asynchronously.
    /// </summary>
    IAsyncEnumerable<LogEvent> ReadHighPriorityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads normal-priority log events asynchronously.
    /// </summary>
    IAsyncEnumerable<LogEvent> ReadNormalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the queue as complete, signaling that no more items will be enqueued.
    /// Used during graceful shutdown.
    /// </summary>
    void Complete();

    /// <summary>
    /// Attempts to synchronously read all remaining high-priority logs (used during shutdown drain).
    /// </summary>
    IEnumerable<LogEvent> DrainHighPriority();

    /// <summary>
    /// Attempts to synchronously read all remaining normal-priority logs (used during shutdown drain).
    /// </summary>
    IEnumerable<LogEvent> DrainNormal();
}
