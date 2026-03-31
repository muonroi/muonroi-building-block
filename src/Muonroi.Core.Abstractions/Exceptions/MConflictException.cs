namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a request conflicts with the current state of the system.
/// </summary>
public sealed class MConflictException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MConflictException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="errorCode">The optional error code.</param>
    public MConflictException(string message, string? errorCode = null)
        : base(errorCode ?? "CONFLICT", message, MExceptionCategory.Domain, 409)
    {
    }
}
