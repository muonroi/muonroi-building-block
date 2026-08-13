using System.Runtime.CompilerServices;

namespace Muonroi.Logging.Abstractions;

/// <summary>
/// Defines a logging interface that extends <see cref="ILogger"/>.
/// </summary>
public interface IMLog : ILogger
{
    /// <summary>
    /// Begins a logging scope with the specified property key and value.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <param name="value">The property value.</param>
    /// <returns>A scope object that should be disposed to end the scope.</returns>
    IMLogContextScope BeginProperty(string key, object? value);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="args">The message arguments.</param>
    void Info(string messageTemplate, params object?[] args);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="args">The message arguments.</param>
    void Warn(string messageTemplate, params object?[] args);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="ex">The exception related to the error, if any.</param>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="args">The message arguments.</param>
    void Error(Exception? ex, string messageTemplate, params object?[] args);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="args">The message arguments.</param>
    void Debug(string messageTemplate, params object?[] args);

    /// <summary>
    /// Logs with an explicit trace node name override (for rule-level context).
    /// </summary>
    void InfoTrace(string messageTemplate, params object?[] args);

    /// <summary>
    /// Logs an informational event with caller context and structured request/result tracking.
    /// </summary>
    void InfoContext(string message, object? request = null, object? result = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0);

    /// <summary>
    /// Logs an error event with caller context and structured tracking.
    /// </summary>
    void ErrorContext(Exception ex, string message, object? request = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0);

    /// <summary>
    /// Logs a business audit event.
    /// </summary>
    void Audit(string action, string objectType, string objectId, bool isSuccess = true, string? previousStatus = null, string? newStatus = null, object? extraData = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0);
}

/// <summary>
/// Defines a generic logging interface that extends <see cref="ILogger{TCategoryName}"/> and <see cref="IMLog"/>.
/// </summary>
/// <typeparam name="T">The type whose name is used for the logger category.</typeparam>
public interface IMLog<out T> : IMLog, ILogger<T>
{
}

/// <summary>
/// Defines a factory interface for creating <see cref="IMLog"/> instances.
/// </summary>
public interface IMLogFactory : IDisposable
{
    /// <summary>
    /// Creates a new <see cref="IMLog"/> instance for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create the logger for.</typeparam>
    /// <returns>A new <see cref="IMLog{T}"/> instance.</returns>
    IMLog<T> CreateLogger<T>();

    /// <summary>
    /// Creates a new <see cref="IMLog"/> instance for the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <returns>A new <see cref="IMLog"/> instance.</returns>
    IMLog CreateLogger(string categoryName);
}
