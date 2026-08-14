namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a request lacks valid authentication credentials.
/// Carries full caller context for diagnostics. Distinct from Muonroi.Mediator's
/// version — this is the Core.Abstractions version used by MGuard.Authorized().
/// </summary>
public sealed class MUnauthorizedException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MUnauthorizedException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="callerMember">Compiler-injected: name of the calling member.</param>
    /// <param name="callerFile">Compiler-injected: source file path of the caller.</param>
    /// <param name="callerLine">Compiler-injected: source line number of the caller.</param>
    public MUnauthorizedException(
        string message,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
        : base("UNAUTHORIZED", message, MExceptionCategory.Security, 401)
    {
        CallerMethod = callerMember;
        CallerFile = callerFile;
        CallerLine = callerLine;
        SourcePackage = ExtractPackageName(callerFile);
    }
}
