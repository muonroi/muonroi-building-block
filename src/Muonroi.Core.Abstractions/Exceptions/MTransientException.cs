namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception representing a transient failure that can be safely retried.
/// </summary>
public sealed class MTransientException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MTransientException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="innerException">The original cause of the transient error.</param>
    public MTransientException(string message, Exception? innerException = null)
        : base("TRANSIENT_ERROR", message, MExceptionCategory.Infrastructure, 503, innerException)
    {
    }
}
