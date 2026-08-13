using Microsoft.Extensions.ObjectPool;
using Muonroi.Logging.Abstractions.Models;

namespace Muonroi.Logging.Queueing;

/// <summary>
/// A policy for pooling <see cref="LogEvent"/> instances.
/// </summary>
public sealed class LogEventPooledObjectPolicy : IPooledObjectPolicy<LogEvent>
{    /// <inheritdoc />
    public LogEvent Create()
    {
        return new LogEvent();
    }
    /// <inheritdoc />
    public bool Return(LogEvent obj)
    {
        obj.Reset();
        return true;
    }
}
