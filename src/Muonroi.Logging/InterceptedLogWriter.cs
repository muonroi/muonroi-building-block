using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Muonroi.Logging.Abstractions;
using Muonroi.Logging.Abstractions.Models;
using Muonroi.Logging.Queueing;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Logging;

/// <summary>
/// Default implementation of <see cref="IInterceptedLogWriter"/> that delegates to the underlying logger or background queue.
/// </summary>
public sealed class InterceptedLogWriter(
    IMuonroiLogQueue? queue = null,
    ObjectPool<LogEvent>? logEventPool = null,
    DiskBufferStore? diskBufferStore = null) : IInterceptedLogWriter
{
    private readonly IMuonroiLogQueue? _queue = queue;
    private readonly ObjectPool<LogEvent>? _logEventPool = logEventPool;
    private readonly DiskBufferStore? _diskBufferStore = diskBufferStore;

    /// <inheritdoc />
    public void Write(string categoryName, LogLevel level, ILogger logger, string messageTemplate, params object?[] properties)
    {
        if (!logger.IsEnabled(level))
        {
            return;
        }

        if (_queue == null || _logEventPool == null)
        {
            logger.Log(level, messageTemplate, properties);
            return;
        }

        Enqueue(categoryName, level, null, messageTemplate, properties);
    }

    /// <inheritdoc />
    public void Write(string categoryName, LogLevel level, ILogger logger, Exception exception, string messageTemplate, params object?[] properties)
    {
        if (!logger.IsEnabled(level))
        {
            return;
        }

        if (_queue == null || _logEventPool == null)
        {
            logger.Log(level, exception, messageTemplate, properties);
            return;
        }

        Enqueue(categoryName, level, exception, messageTemplate, properties);
    }

    private void Enqueue(string categoryName, LogLevel level, Exception? exception, string messageTemplate, object?[] properties)
    {
        LogEvent logEvent = MGuard.NotNull(_logEventPool).Get();
        logEvent.CategoryName = categoryName;
        logEvent.Level = level;
        logEvent.Exception = exception;
        logEvent.MessageTemplate = messageTemplate;
        logEvent.Timestamp = DateTimeOffset.UtcNow;
        logEvent.Properties = properties;

        // Route to appropriate queue
        if (level >= LogLevel.Error || messageTemplate.StartsWith("Audit:", StringComparison.OrdinalIgnoreCase))
        {
            if (!MGuard.NotNull(_queue).TryEnqueueHighPriority(logEvent))
            {
                // Queue is full or closed. Fallback to disk synchronously to avoid dropping high priority events.
                if (_diskBufferStore != null)
                {
                    try
                    {
                        _diskBufferStore.WriteBatch([logEvent], "fallback_high");
                    }
                    catch
                    {
                        // Last resort: drop, but prevent crashing the caller.
                    }
                }
                MGuard.NotNull(_logEventPool).Return(logEvent);
            }
        }
        else
        {
            if (!MGuard.NotNull(_queue).TryEnqueueNormal(logEvent))
            {
                // Queue is full (Wait mode) or closed. For normal logs, we drop them to avoid OOM or disk spam.
                // Or if it's shutdown, we can fallback to disk. Let's do a fast disk fallback.
                if (_diskBufferStore != null)
                {
                    try
                    {
                        _diskBufferStore.WriteBatch([logEvent], "fallback_normal");
                    }
                    catch
                    {
                        // Ignore
                    }
                }
                MGuard.NotNull(_logEventPool).Return(logEvent);
            }
        }
    }
}
