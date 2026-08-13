namespace Muonroi.Logging.Abstractions.Exceptions;

/// <summary>
/// The routing decision for a single exception: what kind of failure it is,
/// a stable machine <see cref="ErrorCode"/>, and whether it is retryable.
/// </summary>
/// <param name="ErrorCode">The stable machine error code.</param>
/// <param name="Retryable">Whether the error is transient and can be retried.</param>
/// <param name="PublicMessage">A message safe to display to users.</param>
public sealed record ExceptionClassification(
    string ErrorCode,
    bool Retryable = false,
    string? PublicMessage = null)
{
    /// <summary>
    /// Returns an unknown exception classification for unhandled errors.
    /// </summary>
    public static ExceptionClassification Unknown(Exception exception)
    {
        return new ExceptionClassification("SYS-UNK-9999", false, "An unexpected error occurred.");
    }
}
