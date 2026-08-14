namespace Muonroi.Logging.Abstractions.Models;

/// <summary>
/// Represents a structured log event suitable for queueing and buffering.
/// </summary>
public sealed class LogEvent
{
    /// <summary>
    /// Gets or sets the timestamp of the event.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the logger category name. Required to resolve ILogger from ILoggerFactory when dequeuing.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message template.
    /// </summary>
    public string MessageTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the log properties.
    /// </summary>
    public object?[] Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets the exception, if any.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Resets the log event for object pooling to reduce GC pressure.
    /// </summary>
    public void Reset()
    {
        Timestamp = default;
        Level = LogLevel.None;
        CategoryName = string.Empty;
        MessageTemplate = string.Empty;
        Properties = [];
        Exception = null;
    }
}
