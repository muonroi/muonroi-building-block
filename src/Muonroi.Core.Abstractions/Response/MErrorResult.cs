namespace Muonroi.Core.Abstractions.Response;

/// <summary>
/// Represents an error result.
/// </summary>
[NotMapped]
public class MErrorResult
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the list of error values.
    /// </summary>
    public List<object> ErrorValues { get; set; } = [];

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return ErrorValues is { Count: > 0 }
            ? "[" + ErrorCode + ": " + ErrorMessage + " (" + string.Join(',', ErrorValues) + ")]"
            : "[" + ErrorCode + ": " + ErrorMessage + "]";
    }
}
