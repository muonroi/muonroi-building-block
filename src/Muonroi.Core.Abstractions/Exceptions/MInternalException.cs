namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception representing an unexpected internal state or invariant violation.
/// Used for "should never happen" scenarios and assertion failures.
/// </summary>
public sealed class MInternalException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MInternalException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="errorCode">Optional specific error code (defaults to INTERNAL_ERROR).</param>
    public MInternalException(string message, string? errorCode = null)
        : base(errorCode ?? "INTERNAL_ERROR", message, MExceptionCategory.Domain, 500)
    {
    }
}
