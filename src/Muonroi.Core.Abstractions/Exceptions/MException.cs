using System.Diagnostics;
using Muonroi.Core.Abstractions.Diagnostics;

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
    /// Gets the upstream causal chain from cross-service error propagation.
    /// Auto-populated from <see cref="Activity.Current"/> baggage (key: "muonroi.causal")
    /// when a <see cref="MCausalChain"/> has been stored
    /// by an upstream <c>MCausalChainDelegatingHandler</c>.
    /// </summary>
    public MCausalChain? CausalChain { get; } =
        Activity.Current?.GetBaggageItem("muonroi.causal") is string baggageValue
            ? MCausalChain.TryDeserialize(baggageValue)
            : null;

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
    /// Gets the architectural layer where this exception originated.
    /// Auto-derived from <see cref="SourcePackage"/> via prefix matching.
    /// </summary>
    public MExceptionLayer Layer => DeriveLayer(SourcePackage);

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
    protected internal static string? ExtractPackageName(string? callerFilePath)
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

    /// <summary>
    /// Determines the architectural layer from a Muonroi package name.
    /// Checks Presentation-specific packages (.Web suffix) BEFORE generic prefixes
    /// to prevent false Domain classification per D-04.
    /// </summary>
    /// <param name="sourcePackage">The package name (e.g., "Muonroi.RuleEngine.Runtime").</param>
    /// <returns>The <see cref="MExceptionLayer"/> for the package.</returns>
    protected internal static MExceptionLayer DeriveLayer(string? sourcePackage)
    {
        if (string.IsNullOrEmpty(sourcePackage))
            return MExceptionLayer.Unknown;

        // Per D-04: Check Presentation-specific .Web packages FIRST
        // to avoid false Domain classification for RuleEngine.*.Web.*
        if (sourcePackage.StartsWith("Muonroi.AspNetCore", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Grpc", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Bff", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.RuleEngine.Runtime.Web", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.RuleEngine.DecisionTable.Web", StringComparison.Ordinal))
            return MExceptionLayer.Presentation;

        if (sourcePackage.StartsWith("Muonroi.Mediator", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.BackgroundJobs", StringComparison.Ordinal))
            return MExceptionLayer.Application;

        if (sourcePackage.StartsWith("Muonroi.RuleEngine", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Rules", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Tenancy", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Auth", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.AuthZ", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Governance", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Core", StringComparison.Ordinal))
            return MExceptionLayer.Domain;

        if (sourcePackage.StartsWith("Muonroi.Data", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Caching", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Messaging", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Logging", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Resilience", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Integration", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Http", StringComparison.Ordinal) ||
            sourcePackage.StartsWith("Muonroi.Diagnostics", StringComparison.Ordinal))
            return MExceptionLayer.Infrastructure;

        return MExceptionLayer.Unknown;
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
