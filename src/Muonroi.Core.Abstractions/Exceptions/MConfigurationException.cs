namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a required configuration value is missing or invalid.
/// Represents startup or infrastructure configuration failures.
/// Automatically captures caller context via compiler-injected attributes.
/// </summary>
public class MConfigurationException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="configKey">The configuration key that is missing or invalid.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    public MConfigurationException(
        string message,
        string? configKey = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        : base("CONFIGURATION_ERROR", message, MExceptionCategory.Infrastructure, 500)
    {
        CallerMethod = callerMember;
        CallerFile = callerFile;
        CallerLine = callerLine;
        SourcePackage = ExtractPackageName(callerFile);

        Details["ConfigKey"] = configKey;
        Details["CallerMethod"] = callerMember;
        Details["CallerFile"] = callerFile;
        Details["CallerLine"] = callerLine;
    }
}
