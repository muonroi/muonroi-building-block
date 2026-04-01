namespace Muonroi.Core.Abstractions.Response;

/// <summary>
/// Standardized error response contract for all Muonroi APIs.
/// </summary>
public class MErrorResponse
{
    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the machine-readable error code.
    /// </summary>
    public string ErrorCode { get; set; } = "INTERNAL_ERROR";

    /// <summary>
    /// Gets or sets the unique trace identifier for the request.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets field-level validation errors.
    /// </summary>
    public List<MErrorDetail>? Errors { get; set; }

    /// <summary>
    /// Gets or sets additional structured context (usually for internal/dev use).
    /// </summary>
    public Dictionary<string, object?>? Details { get; set; }
}

/// <summary>
/// Represents a single field-level error.
/// </summary>
public class MErrorDetail
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value that caused the error (Dev environment only).
    /// </summary>
    public object? AttemptedValue { get; set; }
}
