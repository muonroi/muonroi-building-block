namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when the caller is authenticated but lacks required permissions.
/// Carries full caller context for diagnostics. Distinct from Muonroi.Mediator's
/// version — this is the Core.Abstractions version used by MGuard.Permitted().
/// </summary>
public sealed class MForbiddenException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MForbiddenException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    public MForbiddenException(
        string message,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        : base("FORBIDDEN", message, MExceptionCategory.Security, 403)
    {
        CallerMethod = callerMember;
        CallerFile = callerFile;
        CallerLine = callerLine;
        SourcePackage = ExtractPackageName(callerFile);
    }
}
