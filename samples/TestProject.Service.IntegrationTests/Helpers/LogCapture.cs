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
    private readonly List<LogEntry> _entries = new();

    /// <summary>All captured log entries since this instance was created.</summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, _entries);

    public void Dispose() { }

    /// <summary>Returns true if any captured entry contains the given substring (case-insensitive).</summary>
    public bool HasMessage(string substring)
        => _entries.Any(e => e.Message.Contains(substring, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns all entries whose message contains the given substring (case-insensitive).</summary>
    public IEnumerable<LogEntry> FindEntries(string substring)
        => _entries.Where(e => e.Message.Contains(substring, StringComparison.OrdinalIgnoreCase));

    /// <summary>Clears all captured entries.</summary>
    public void Clear() => _entries.Clear();

    private sealed class CaptureLogger(string category, List<LogEntry> entries) : ILogger
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
            entries.Add(new LogEntry
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
