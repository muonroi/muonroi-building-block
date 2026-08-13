namespace Muonroi.Logging.Abstractions;

/// <summary>
/// Resolves log arguments to safely serialize them, apply PII masking, 
/// and enforce payload size limits before logging.
/// </summary>
public interface IMLogArgumentResolver
{
    /// <summary>
    /// Safely resolves a log argument (request or result), applying masking 
    /// and size truncation if necessary.
    /// </summary>
    /// <param name="argument">The raw argument.</param>
    /// <returns>A safe representation of the argument (e.g., masked JSON string or primitive).</returns>
    object? Resolve(object? argument);
}
