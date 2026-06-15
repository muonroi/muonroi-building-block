namespace TestProject.Service.IntegrationTests.Helpers;

/// <summary>
/// Represents a single captured log entry, including active scope data at the time of logging.
/// </summary>
public sealed class LogEntry
{
    public LogLevel Level { get; init; }
    public string Category { get; init; } = "";
    public string Message { get; init; } = "";
    public Exception? Exception { get; init; }

    /// <summary>
    /// Snapshot of the active scope key-value pairs at the time this entry was logged.
    /// Populated when BeginScope is called with an IEnumerable&lt;KeyValuePair&lt;string, object?&gt;&gt;.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Scope { get; init; }
}

/// <summary>
/// In-memory ILoggerProvider that captures all log entries for assertion in tests.
/// Register via: services.AddLogging(b => b.AddProvider(logCapture))
/// </summary>
public sealed class LogCapture : ILoggerProvider
{
    // The ASP.NET TestServer host logs from background threads (Kestrel/host lifetime/etc.) while the
    // test thread reads the captured entries, so every access to this list must be synchronized.
    // Reads materialize a snapshot under the lock — returning a lazy LINQ query over the live list
    // would re-enumerate it outside the lock and throw "Collection was modified" when a concurrent
    // log write Adds during enumeration.
    private readonly List<LogEntry> _entries = new();
    private readonly object _sync = new();

    /// <summary>All captured log entries since this instance was created (point-in-time snapshot).</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_sync) { return _entries.ToArray(); } }
    }

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Add);

    public void Dispose() { }

    private void Add(LogEntry entry)
    {
        lock (_sync) { _entries.Add(entry); }
    }

    /// <summary>Returns true if any captured entry contains the given substring (case-insensitive).</summary>
    public bool HasMessage(string substring)
    {
        lock (_sync)
        {
            return _entries.Any(e => e.Message.Contains(substring, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Returns all entries whose message contains the given substring (case-insensitive).</summary>
    public IReadOnlyList<LogEntry> FindEntries(string substring)
    {
        lock (_sync)
        {
            return _entries
                .Where(e => e.Message.Contains(substring, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    /// <summary>Clears all captured entries.</summary>
    public void Clear()
    {
        lock (_sync) { _entries.Clear(); }
    }

    private sealed class CaptureLogger(string category, Action<LogEntry> add) : ILogger
    {
        // Tracks the active scope key-value pairs for this logger instance.
        // CaptureLogger is created per-category so this is safe for single-threaded test pipelines.
        private Dictionary<string, object?>? _activeScope;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> dict)
            {
                var prev = _activeScope;
                _activeScope = dict.ToDictionary(k => k.Key, v => v.Value);
                return new ScopeDisposable(() => _activeScope = prev);
            }
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            add(new LogEntry
            {
                Level = logLevel,
                Category = category,
                Message = formatter(state, exception),
                Exception = exception,
                // Snapshot the current scope at log time
                Scope = _activeScope is not null
                    ? new Dictionary<string, object?>(_activeScope)
                    : null
            });
        }

        /// <summary>Simple disposable that runs a cleanup action when disposed.</summary>
        private sealed class ScopeDisposable(Action onDispose) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    onDispose();
                }
            }
        }
    }
}
