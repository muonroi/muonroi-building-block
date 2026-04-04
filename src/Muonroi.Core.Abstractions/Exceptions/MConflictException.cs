namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a request conflicts with the current state of the system.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MConflictException"/> class.
/// </remarks>
/// <param name="message">The human-readable error message.</param>
/// <param name="errorCode">The optional error code.</param>
public sealed class MConflictException(string message, string? errorCode = null) : MException(errorCode ?? "CONFLICT", message, MExceptionCategory.Domain, 409)
{
}
