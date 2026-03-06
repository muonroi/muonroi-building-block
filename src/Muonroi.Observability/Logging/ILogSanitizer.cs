namespace Muonroi.Observability.Logging;

/// <summary>
/// Sanitizes log data by redacting sensitive fields.
/// </summary>
public interface ILogSanitizer
{
    /// <summary>
    /// Returns a sanitized copy of the provided log data.
    /// </summary>
    IDictionary<string, object?> Sanitize(IDictionary<string, object?> data);
}
