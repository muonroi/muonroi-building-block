namespace Muonroi.Core.Abstractions.Response;

/// <summary>
/// Standardized error template for machine readable responses.
/// </summary>
/// <param name="Code">Error code.</param>
/// <param name="Message">Human readable error message.</param>
/// <param name="Details">Optional additional error details.</param>
public sealed record ApiError(string Code, string Message, IDictionary<string, object>? Details = null);
