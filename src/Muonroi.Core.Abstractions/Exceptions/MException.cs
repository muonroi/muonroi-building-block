using System.Diagnostics;

namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Abstract base class for all exceptions in the Muonroi ecosystem.
/// Automatically captures caller context (method, file, line), trace context
/// (TraceId, SpanId), and source package information.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MException"/> class.
/// </remarks>
/// <param name="errorCode">The machine-readable error code (format: MBB:PKG:NNN).</param>
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
    /// Gets the distributed trace identifier (W3C TraceId).
    /// Automatically captured from <see cref="Activity.Current"/> at exception creation time.
    /// </summary>
    public string? TraceId { get; } = Activity.Current?.TraceId.ToString();

    /// <summary>
    /// Gets the distributed span identifier (W3C SpanId).
    /// Automatically captured from <see cref="Activity.Current"/> at exception creation time.
    /// </summary>
    public string? SpanId { get; } = Activity.Current?.SpanId.ToString();

    /// <summary>
    /// Gets the name of the Muonroi package that threw this exception.
    /// Derived from the <see cref="CallerFile"/> path — AOT-safe, no reflection.
    /// Example: "Muonroi.RuleEngine.Runtime"
    /// </summary>
    public string? SourcePackage { get; init; }

    /// <summary>
    /// Gets the name of the method that created this exception.
    /// Populated via [CallerMemberName] through subclass constructors or MGuard methods.
    /// </summary>
    public string? CallerMethod { get; init; }

    /// <summary>
    /// Gets the source file path of the caller that created this exception.
    /// Populated via [CallerFilePath] through subclass constructors or MGuard methods.
    /// </summary>
    public string? CallerFile { get; init; }

    /// <summary>
    /// Gets the source line number of the caller that created this exception.
    /// Populated via [CallerLineNumber] through subclass constructors or MGuard methods.
    /// </summary>
    public int CallerLine { get; init; }

    /// <summary>
    /// Gets additional structured context for the exception.
    /// </summary>
    public Dictionary<string, object?> Details { get; } = [];

    /// <summary>
    /// Extracts the Muonroi package name from a CallerFilePath string.
    /// Scans path segments for a segment starting with "Muonroi." and returns the first match.
    /// AOT-safe: uses string parsing only, no reflection or assembly loading.
    /// </summary>
    /// <param name="callerFilePath">The full source file path from [CallerFilePath].</param>
    /// <returns>
    /// The package directory segment (e.g., "Muonroi.RuleEngine.Runtime"),
    /// or <see langword="null"/> if the path does not contain a Muonroi package segment.
    /// </returns>
    protected static string? ExtractPackageName(string? callerFilePath)
    {
        if (string.IsNullOrEmpty(callerFilePath))
        {
            return null;
        }

        // Normalize path separators, then scan segments for a Muonroi.* directory
        // Use string.Replace for compatibility (ReadOnlySpan.Replace not available on all TFMs)
        string normalizedPath = callerFilePath.Replace('\\', '/');
        int start = 0;

        while (start < normalizedPath.Length)
        {
            int slash = normalizedPath.IndexOf('/', start);
            string segment = slash < 0
                ? normalizedPath[start..]
                : normalizedPath.Substring(start, slash - start);

            if (segment.StartsWith("Muonroi.", StringComparison.Ordinal))
            {
                return segment;
            }

            start = slash < 0 ? normalizedPath.Length : slash + 1;
        }

        return null;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var correlationStr = !string.IsNullOrEmpty(CorrelationId) ? $" [CorrelationId: {CorrelationId}]" : string.Empty;
        var sourceStr = !string.IsNullOrEmpty(SourcePackage) ? $" [{SourcePackage}]" : string.Empty;
        var callerStr = !string.IsNullOrEmpty(CallerMethod) ? $" at {CallerMethod}" : string.Empty;
        return $"{GetType().Name} ({Category}): {ErrorCode} - {Message}{sourceStr}{callerStr}{correlationStr}";
    }
}
