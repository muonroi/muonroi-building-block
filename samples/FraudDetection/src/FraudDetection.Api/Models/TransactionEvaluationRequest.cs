namespace FraudDetection.Api.Models;

public sealed record TransactionEvaluationRequest
{
    public string TransactionId { get; init; } = string.Empty;
    public string CardId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string MerchantId { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow; // MBB001-exempt: sample DTO boundary default
}
