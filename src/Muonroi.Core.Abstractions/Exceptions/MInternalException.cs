using System.Runtime.CompilerServices;

namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception representing an unexpected internal state or invariant violation.
/// Used for "should never happen" scenarios and assertion failures.
/// Automatically captures caller context via compiler-injected attributes.
/// </summary>
public class MInternalException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MInternalException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="errorCode">Optional specific error code (defaults to INTERNAL_ERROR).</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    public MInternalException(
        string message,
        string? errorCode = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        : base(errorCode ?? "INTERNAL_ERROR", message, MExceptionCategory.Domain, 500)
    {
        CallerMethod = callerMember;
        CallerFile = callerFile;
        CallerLine = callerLine;
        SourcePackage = ExtractPackageName(callerFile);

        Details["CallerMethod"] = callerMember;
        Details["CallerFile"] = callerFile;
        Details["CallerLine"] = callerLine;
    }
}
