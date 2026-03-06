namespace Muonroi.Logging.Abstractions;

public interface IMLog<T> : ILogger<T>
{
    IMLogContextScope BeginProperty(string key, object? value);
    void Info(string messageTemplate, params object[] args);
    void Warn(string messageTemplate, params object[] args);
    void Error(Exception? ex, string messageTemplate, params object[] args);
    void Debug(string messageTemplate, params object[] args);
}
