namespace Muonroi.Core.Abstractions.Constants;

/// <summary>
/// Defines constants for custom HTTP header names used in the application.
/// </summary>
public static class CustomHeader
{
    /// <summary>
    /// The header name for the correlation identifier.
    /// </summary>
    public const string CorrelationId = "x-correlation-id";

    /// <summary>
    /// The header name for the API key.
    /// </summary>
    public const string ApiKey = "x-api-key";

    /// <summary>
    /// The header name for the tenant identifier.
    /// </summary>
    public const string TenantId = "x-tenant-id";

    /// <summary>
    /// The header name for the timestamp when the request was sent.
    /// </summary>
    public const string SentAt = "x-sent-at";

    /// <summary>
    /// The header name for the source type of the request.
    /// </summary>
    public const string SourceType = "x-source-type";
}
