using System.Collections.Concurrent;
using Muonroi.RuleEngine.CEP;
using Muonroi.RuleEngine.CEP.Abstractions;
using Muonroi.RuleEngine.CEP.Builder;
using FraudDetection.Api.Models;

namespace FraudDetection.Api.Services;

/// <summary>
/// Represents the Fraud Monitor Service.
/// </summary>
public sealed class FraudMonitorService(ICepConfigRepository repository)
{
    /// <summary>
    /// The Default Config Id.
    /// </summary>
    public const string DefaultConfigId = "high-velocity-cards";

    private readonly ConcurrentDictionary<string, WindowRegistration> _windows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Executes the Evaluate Async operation.
    /// </summary>
    public async Task<FraudEvaluationResult> EvaluateAsync(
        TransactionEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        CepConfig config = await repository.GetAsync(DefaultConfigId, cancellationToken)
            ?? throw new InvalidOperationException($"CEP config '{DefaultConfigId}' is missing for the current tenant.");

        string registrationKey = $"{config.TenantId}:{config.Id}";
        WindowRegistration registration = _windows.AddOrUpdate(
            registrationKey,
            _ => CreateRegistration(config),
            (_, existing) => existing.Version == config.UpdatedAtUtc.Ticks ? existing : CreateRegistration(config));

        IReadOnlyList<CepEvent<TransactionEvaluationRequest>> activeWindow = registration.Window.Add(
            request.CardId,
            request,
            request.TimestampUtc.Kind == DateTimeKind.Utc ? request.TimestampUtc : request.TimestampUtc.ToUniversalTime());

        int threshold = ParseThreshold(config.Metadata);
        decimal totalAmount = activeWindow.Sum(x => x.Value.Amount);

        return new FraudEvaluationResult
        {
            TenantId = config.TenantId,
            CardId = request.CardId,
            AlertTriggered = activeWindow.Count >= threshold,
            EventCount = activeWindow.Count,
            Threshold = threshold,
            WindowSizeSeconds = (int)Math.Round(config.WindowSize.TotalSeconds),
            TotalAmount = totalAmount
        };
    }

    private static WindowRegistration CreateRegistration(CepConfig config)
    {
        CepWindow<TransactionEvaluationRequest> window = CepWindowBuilder
            .For<TransactionEvaluationRequest>(config)
            .CorrelateBy(x => x.CardId)
            .Build();

        return new WindowRegistration(config.UpdatedAtUtc.Ticks, window);
    }

    private static int ParseThreshold(IReadOnlyDictionary<string, string> metadata)
    {
        return metadata.TryGetValue("threshold", out string? raw) && int.TryParse(raw, out int value) && value > 0
            ? value
            : 3;
    }

    private sealed record WindowRegistration(long Version, CepWindow<TransactionEvaluationRequest> Window);
}

/// <summary>
/// Represents the Fraud Evaluation Result.
/// </summary>
public sealed record FraudEvaluationResult
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
