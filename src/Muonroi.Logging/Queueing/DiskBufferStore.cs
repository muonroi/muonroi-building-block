namespace Muonroi.Logging.Queueing;

/// <summary>
/// Handles disk buffering for log events to ensure zero-data-loss when memory queues are full or during graceful shutdown.
/// </summary>
public sealed class DiskBufferStore
{
    private readonly string _bufferDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _writeLock = new();

    private sealed class BufferedLogEventDto
    {
        public DateTimeOffset Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string MessageTemplate { get; set; } = string.Empty;
        public string[] Properties { get; set; } = [];
        public string? ExceptionString { get; set; }
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="DiskBufferStore"/> class.
    /// </summary>
    /// <param name="bufferDirectory">The directory to store log buffers. Defaults to `.logbuffer`.</param>
    public DiskBufferStore(string? bufferDirectory = null)
    {
        _bufferDirectory = bufferDirectory ?? Path.Combine(AppContext.BaseDirectory, ".logbuffer");
        
        if (!Directory.Exists(_bufferDirectory))
        {
            Directory.CreateDirectory(_bufferDirectory);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Synchronously writes a batch of log events to a new buffer file.
    /// Typically used during shutdown drain when async is not reliable.
    /// </summary>
    public void WriteBatch(IReadOnlyCollection<LogEvent> events, string prefix = "drain")
    {
        if (events.Count == 0) return;

        string filePath = Path.Combine(_bufferDirectory, $"{prefix}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_ffff}_{Guid.NewGuid():N}.logbuffer");
        
        try
        {
            lock (_writeLock)
            {
                using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                
                foreach (LogEvent logEvent in events)
                {
                    var dto = new BufferedLogEventDto
                    {
                        Timestamp = logEvent.Timestamp,
                        Level = logEvent.Level,
                        CategoryName = logEvent.CategoryName,
                        MessageTemplate = logEvent.MessageTemplate,
                        Properties = logEvent.Properties.Select(p => p?.ToString() ?? string.Empty).ToArray(),
                        ExceptionString = logEvent.Exception?.ToString()
                    };
                    
                    string json = JsonSerializer.Serialize(dto, _jsonOptions);
                    writer.WriteLine(json);
                }
            }
        }
        catch (Exception ex)
        {
            // Failsafe: Output to console if disk write fails to avoid crashing caller
            Console.Error.WriteLine($"[Muonroi.Logging] FATAL: Failed to write to disk buffer '{filePath}'. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads and deletes orphaned buffer files from previous runs.
    /// </summary>
    public IEnumerable<LogEvent> ReadAndCleanupOrphanedBuffers()
    {
        if (!Directory.Exists(_bufferDirectory)) yield break;

        string[] files = Directory.GetFiles(_bufferDirectory, "*.logbuffer");
        foreach (string file in files)
        {
            List<LogEvent>? events = ReadFile(file);
            if (events == null) continue;

            foreach (LogEvent e in events)
            {
                yield return e;
            }

            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignored
            }
        }
    }

    private List<LogEvent>? ReadFile(string file)
    {
        var list = new List<LogEvent>();
        try
        {
            foreach (string line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                BufferedLogEventDto? dto = null;
                try
                {
                    dto = JsonSerializer.Deserialize<BufferedLogEventDto>(line, _jsonOptions);
                }
                catch
                {
                    // Ignore corrupted lines
                }

                if (dto != null)
                {
                    list.Add(new LogEvent
                    {
                        Timestamp = dto.Timestamp,
                        Level = dto.Level,
                        CategoryName = dto.CategoryName,
                        MessageTemplate = dto.MessageTemplate,
                        Properties = dto.Properties.Cast<object?>().ToArray(),
                        Exception = dto.ExceptionString != null ? new Exception("Recovered Exception: " + dto.ExceptionString) : null
                    });
                }
            }
            return list;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Muonroi.Logging] ERROR: Failed to read disk buffer '{file}'. Error: {ex.Message}");
            return null;
        }
    }
}
