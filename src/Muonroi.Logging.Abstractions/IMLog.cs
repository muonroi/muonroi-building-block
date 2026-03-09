namespace Muonroi.Logging.Abstractions;

/// <summary>
/// Defines a generic logging interface that extends <see cref="ILogger{TCategoryName}"/>.
/// </summary>
/// <typeparam name="T">The type whose name is used for the logger category.</typeparam>
public interface IMLog<T> : ILogger<T>
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
}
