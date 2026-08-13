using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Muonroi.Logging.Abstractions.Models;

namespace Muonroi.Logging.Queueing;

/// <summary>
/// A background service that consumes log events from the in-memory queue and flushes them to the underlying logger providers.
/// </summary>
public sealed class LogBackgroundProcessor(
    IMuonroiLogQueue queue,
    DiskBufferStore diskBuffer,
    ILoggerFactory loggerFactory,
    ObjectPool<LogEvent> logEventPool) : BackgroundService
{
    private readonly IMuonroiLogQueue _queue = queue;
    private readonly DiskBufferStore _diskBuffer = diskBuffer;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ObjectPool<LogEvent> _logEventPool = logEventPool;
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately to avoid blocking the host's StartAsync method
        await Task.Yield();

        // 1. Startup Recovery: Drain orphaned disk buffers from previous crashes
        RecoverOrphanedDiskBuffers();

        // 2. Start concurrent readers for High and Normal priority channels
        Task highPriorityTask = ConsumeHighPriorityAsync(stoppingToken);
        Task normalPriorityTask = ConsumeNormalPriorityAsync(stoppingToken);

        try
        {
            await Task.WhenAll(highPriorityTask, normalPriorityTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown
        }
        finally
        {
            // 3. Graceful Shutdown: Stop accepting new logs and drain memory to disk
            DrainMemoryToDisk();
        }
    }

    private void RecoverOrphanedDiskBuffers()
    {
        foreach (LogEvent logEvent in _diskBuffer.ReadAndCleanupOrphanedBuffers())
        {
            ProcessLogEvent(logEvent);
            // We do not return these to the pool as they were instantiated by JSON deserializer, not rented.
        }
    }

    private async Task ConsumeHighPriorityAsync(CancellationToken stoppingToken)
    {
        await foreach (LogEvent logEvent in _queue.ReadHighPriorityAsync(stoppingToken).ConfigureAwait(false))
        {
            ProcessLogEvent(logEvent);
            _logEventPool.Return(logEvent);
        }
    }

    private async Task ConsumeNormalPriorityAsync(CancellationToken stoppingToken)
    {
        await foreach (LogEvent logEvent in _queue.ReadNormalAsync(stoppingToken).ConfigureAwait(false))
        {
            ProcessLogEvent(logEvent);
            _logEventPool.Return(logEvent);
        }
    }

    private void ProcessLogEvent(LogEvent logEvent)
    {
        try
        {
            ILogger logger = _loggerFactory.CreateLogger(string.IsNullOrWhiteSpace(logEvent.CategoryName) ? "Muonroi.Logging" : logEvent.CategoryName);

            if (logEvent.Exception != null)
            {
                logger.Log(logEvent.Level, logEvent.Exception, logEvent.MessageTemplate, logEvent.Properties);
            }
            else
            {
                logger.Log(logEvent.Level, logEvent.MessageTemplate, logEvent.Properties);
            }
        }
        catch
        {
            // Sink failed (e.g. invalid format or disconnected provider). Avoid crashing the background processor.
        }
    }

    private void DrainMemoryToDisk()
    {
        // Tell the queue to stop accepting new items
        _queue.Complete();

        // Drain whatever is left in memory
        List<LogEvent> leftoverHigh = _queue.DrainHighPriority().ToList();
        List<LogEvent> leftoverNormal = _queue.DrainNormal().ToList();

        if (leftoverHigh.Count > 0)
        {
            _diskBuffer.WriteBatch(leftoverHigh, "shutdown_high");
            foreach (var logEvent in leftoverHigh)
            {
                _logEventPool.Return(logEvent);
            }
        }

        if (leftoverNormal.Count > 0)
        {
            _diskBuffer.WriteBatch(leftoverNormal, "shutdown_normal");
            foreach (var logEvent in leftoverNormal)
            {
                _logEventPool.Return(logEvent);
            }
        }
    }
}
