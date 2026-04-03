namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when an argument passed to a method is invalid.
/// </summary>
public sealed class MArgumentException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MArgumentException"/> class.
    /// </summary>
    /// <param name="paramName">The name of the parameter that is invalid.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="callerMember">The member name of the caller.</param>
    /// <param name="callerFile">The source file path of the caller.</param>
    /// <param name="callerLine">The source line number in the caller file.</param>
    public MArgumentException(
        string paramName,
        string message,
        string? callerMember = null,
        string? callerFile = null,
        int callerLine = 0)
        : base("ARGUMENT_ERROR", message, MExceptionCategory.Validation, 400)
    {
        Details["ParamName"] = paramName;
        Details["CallerMember"] = callerMember;
        Details["CallerFile"] = callerFile;
        Details["CallerLine"] = callerLine;
    }
}
