namespace FraudDetection.Api.Models;

/// <summary>
/// Represents the Transaction Evaluation Request.
/// </summary>
public sealed record TransactionEvaluationRequest
{
    /// <summary>
    /// Gets or sets the Transaction Id.
    /// </summary>
    public string TransactionId { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the Card Id.
    /// </summary>
    public string CardId { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the Amount.
    /// </summary>
    public decimal Amount { get; init; }
    /// <summary>
    /// Gets or sets the Merchant Id.
    /// </summary>
    public string MerchantId { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the Country Code.
    /// </summary>
    public string CountryCode { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the Timestamp Utc.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow; // MBB001-exempt: sample DTO boundary default
}
