namespace FraudDetection.Api.Models;

/// <summary>
/// Represents the Transaction Evaluation Response.
/// </summary>
public sealed record TransactionEvaluationResponse
{
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string TenantId { get; init; } = "_global";
    /// <summary>
    /// Gets or sets the Card Id.
    /// </summary>
    public string CardId { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the Alert Triggered.
    /// </summary>
    public bool AlertTriggered { get; init; }
    /// <summary>
    /// Gets or sets the Event Count.
    /// </summary>
    public int EventCount { get; init; }
    /// <summary>
    /// Gets or sets the Threshold.
    /// </summary>
    public int Threshold { get; init; }
    /// <summary>
    /// Gets or sets the Window Size Seconds.
    /// </summary>
    public int WindowSizeSeconds { get; init; }
    /// <summary>
    /// Gets or sets the Total Amount.
    /// </summary>
    public decimal TotalAmount { get; init; }
}
