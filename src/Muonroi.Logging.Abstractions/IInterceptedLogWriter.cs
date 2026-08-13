using Microsoft.Extensions.Logging;

namespace Muonroi.Logging.Abstractions;

/// <summary>
/// A centralized interception pipeline for all logs written through MLog.
/// This allows custom filtering, dynamic log level overrides, and event inspection
/// before logs are delegated to the underlying provider.
/// </summary>
public interface IInterceptedLogWriter
{
    /// <summary>
    /// Evaluates rules and writes the log to the underlying logger if allowed.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <param name="logger">The underlying Microsoft.Extensions.Logging.ILogger instance.</param>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="properties">The resolved property values.</param>
    void Write(LogLevel level, ILogger logger, string messageTemplate, params object?[] properties);

    /// <summary>
    /// Evaluates rules and writes the log with an exception to the underlying logger if allowed.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <param name="logger">The underlying Microsoft.Extensions.Logging.ILogger instance.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="properties">The resolved property values.</param>
    void Write(LogLevel level, ILogger logger, Exception exception, string messageTemplate, params object?[] properties);
}
