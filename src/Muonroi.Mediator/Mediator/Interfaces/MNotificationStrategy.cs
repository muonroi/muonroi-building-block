namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Controls how notification handlers are invoked by <see cref="MMediator"/>.
/// </summary>
public enum MNotificationStrategy
{
    /// <summary>
    /// Handlers are called one-by-one in DI registration order. Default behavior.
    /// </summary>
    Sequential = 0,

    /// <summary>
    /// All handlers are dispatched in parallel and awaited (Task.WhenAll).
    /// Failures in individual handlers are collected and re-thrown as AggregateException.
    /// </summary>
    ParallelWaitAll = 1,

    /// <summary>
    /// Handlers are dispatched via Task.Run and the call returns immediately (fire-and-forget).
    /// Use with caution — exceptions are silently swallowed.
    /// </summary>
    ParallelNoWait = 2,
}

/// <summary>
/// Optional interface that notifications can implement to override the default strategy.
/// </summary>
public interface IMStrategyNotification : INotification
{
    /// <summary>
    /// Gets the Strategy.
    /// </summary>
    MNotificationStrategy Strategy { get; }
}
