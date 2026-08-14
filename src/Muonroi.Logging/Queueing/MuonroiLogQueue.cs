namespace Muonroi.Logging.Queueing;

/// <summary>
/// Implementation of <see cref="IMuonroiLogQueue"/> using <see cref="Channel{T}"/>.
/// </summary>
public sealed class MuonroiLogQueue : IMuonroiLogQueue
{
    private readonly Channel<LogEvent> _highPriorityChannel;
    private readonly Channel<LogEvent> _normalChannel;
    /// <summary>
    /// Initializes a new instance of the <see cref="MuonroiLogQueue"/> class.
    /// </summary>
    public MuonroiLogQueue()
    {
        // High priority queue: Bounded, Wait mode. If full, TryWrite returns false.
        _highPriorityChannel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // LogBackgroundProcessor is the only reader
            SingleWriter = false
        });

        // Normal priority queue: Bounded, Wait mode. If full, TryWrite returns false so caller can return to pool.
        _normalChannel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(50_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public bool TryEnqueueHighPriority(LogEvent logEvent)
    {
        return _highPriorityChannel.Writer.TryWrite(logEvent);
    }

    /// <inheritdoc />
    public bool TryEnqueueNormal(LogEvent logEvent)
    {
        return _normalChannel.Writer.TryWrite(logEvent);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LogEvent> ReadHighPriorityAsync(CancellationToken cancellationToken = default)
    {
        return _highPriorityChannel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LogEvent> ReadNormalAsync(CancellationToken cancellationToken = default)
    {
        return _normalChannel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Complete()
    {
        _highPriorityChannel.Writer.TryComplete();
        _normalChannel.Writer.TryComplete();
    }

    /// <inheritdoc />
    public IEnumerable<LogEvent> DrainHighPriority()
    {
        while (_highPriorityChannel.Reader.TryRead(out LogEvent? logEvent))
        {
            yield return logEvent;
        }
    }

    /// <inheritdoc />
    public IEnumerable<LogEvent> DrainNormal()
    {
        while (_normalChannel.Reader.TryRead(out LogEvent? logEvent))
        {
            yield return logEvent;
        }
    }
}
