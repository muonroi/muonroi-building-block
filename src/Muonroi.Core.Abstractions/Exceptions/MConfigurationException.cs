namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a required configuration value is missing or invalid.
/// Represents startup or infrastructure configuration failures.
/// </summary>
public class MConfigurationException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="configKey">The configuration key that is missing or invalid.</param>
    public MConfigurationException(string message, string? configKey = null)
        : base("CONFIGURATION_ERROR", message, MExceptionCategory.Infrastructure, 500)
    {
        Details["ConfigKey"] = configKey;
    }
}
