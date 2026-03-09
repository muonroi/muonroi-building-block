namespace FraudDetection.Api.Models;

public sealed record TransactionEvaluationResponse
{
    public string TenantId { get; init; } = "_global";
    public string CardId { get; init; } = string.Empty;
    public bool AlertTriggered { get; init; }
    public int EventCount { get; init; }
    public int Threshold { get; init; }
    public int WindowSizeSeconds { get; init; }
    public decimal TotalAmount { get; init; }
}
