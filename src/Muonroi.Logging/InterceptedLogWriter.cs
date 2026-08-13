using Microsoft.Extensions.Logging;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Logging;

/// <summary>
/// Default implementation of <see cref="IInterceptedLogWriter"/> that delegates to the underlying logger.
/// Acts as a central pipeline to apply dynamic logic (like dynamic log level checks) before writing.
/// </summary>
public sealed class InterceptedLogWriter : IInterceptedLogWriter
{
    /// <inheritdoc />
    public void Write(LogLevel level, ILogger logger, string messageTemplate, params object?[] properties)
    {
        // Extension Point: Here we can inject Dynamic Log Level validation in the future.
        // E.g., if (!DynamicLogLevelStore.IsAllowed(level)) return;

        if (!logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(level, messageTemplate, properties);
    }

    /// <inheritdoc />
    public void Write(LogLevel level, ILogger logger, Exception exception, string messageTemplate, params object?[] properties)
    {
        // Extension Point: Here we can inject Dynamic Log Level validation in the future.
        if (!logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(level, exception, messageTemplate, properties);
    }
}
