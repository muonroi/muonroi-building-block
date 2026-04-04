using System.Diagnostics;

namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Abstract base class for all exceptions in the Muonroi ecosystem.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MException"/> class.
/// </remarks>
/// <param name="errorCode">The machine-readable error code.</param>
/// <param name="message">The human-readable error message.</param>
/// <param name="category">The exception category.</param>
/// <param name="httpStatusCode">The recommended HTTP status code.</param>
/// <param name="innerException">Optional inner exception.</param>
public abstract class MException(
    string errorCode,
    string message,
    MExceptionCategory category = MExceptionCategory.Domain,
    int httpStatusCode = 500,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the machine-readable error code.
    /// </summary>
    public string ErrorCode { get; } = errorCode;

    /// <summary>
    /// Gets the category of the exception.
    /// </summary>
    public MExceptionCategory Category { get; } = category;

    /// <summary>
    /// Gets the recommended HTTP status code for this exception.
    /// </summary>
    public int HttpStatusCode { get; } = httpStatusCode;

    /// <summary>
    /// Gets the correlation identifier for tracking this error.
    /// </summary>
    public string? CorrelationId { get; } = Activity.Current?.Id;

    /// <summary>
    /// Gets additional structured context for the exception.
    /// </summary>
    public Dictionary<string, object?> Details { get; } = [];

    /// <inheritdoc />
    public override string ToString()
    {
        var correlationStr = !string.IsNullOrEmpty(CorrelationId) ? $" [CorrelationId: {CorrelationId}]" : string.Empty;
        return $"{GetType().Name} ({Category}): {ErrorCode} - {Message}{correlationStr}";
    }
}
