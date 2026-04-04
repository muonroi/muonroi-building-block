namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception representing a transient failure that can be safely retried.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MTransientException"/> class.
/// </remarks>
/// <param name="message">The human-readable error message.</param>
/// <param name="innerException">The original cause of the transient error.</param>
public sealed class MTransientException(string message, Exception? innerException = null) : MException("TRANSIENT_ERROR", message, MExceptionCategory.Infrastructure, 503, innerException)
{
}
