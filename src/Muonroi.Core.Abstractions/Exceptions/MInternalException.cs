namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception representing an unexpected internal state or invariant violation.
/// Used for "should never happen" scenarios and assertion failures.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MInternalException"/> class.
/// </remarks>
/// <param name="message">The human-readable error message.</param>
/// <param name="errorCode">Optional specific error code (defaults to INTERNAL_ERROR).</param>
public class MInternalException(string message, string? errorCode = null) : MException(errorCode ?? "INTERNAL_ERROR", message, MExceptionCategory.Domain, 500)
{
}
