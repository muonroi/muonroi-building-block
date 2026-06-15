using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Logging.Abstractions;
using Muonroi.Observability.OpenTelemetry;
using Polly;
using Polly.CircuitBreaker;

namespace Quickstart.Resilience.Api.Services;

/// <summary>
/// Demonstrates resilience patterns for a payment-gateway integration.
///
/// Two scenarios are covered:
/// <list type="bullet">
///   <item>
///     <term>Transient failure</term>
///     <description>
///       Throwing <see cref="MTransientException"/> triggers the retry predicate in the
///       "muonroi-standard" pipeline (up to 3 attempts, exponential back-off).
///     </description>
///   </item>
///   <item>
///     <term>Circuit breaker</term>
///     <description>
///       Throwing a generic <see cref="Exception"/> also matches the circuit-breaker predicate.
///       After 5+ operations with ≥50% failures in a 30-second window the breaker opens for 30s,
///       causing subsequent calls to fail fast with <see cref="BrokenCircuitException"/>.
///     </description>
///   </item>
/// </list>
/// </summary>
public sealed class PaymentService
{
    private readonly ResiliencePipeline _pipeline;
    private readonly IMLog<PaymentService> _logger;

    // Counts consecutive hard failures injected for the circuit-breaker demo.
    private int _hardFailureCount;

    public PaymentService(
        ResiliencePipelineProvider<string> pipelineProvider,
        IMLog<PaymentService> logger)
    {
        // The "muonroi-standard" pipeline is registered by AddMuonroiResilience().
        _pipeline = pipelineProvider.GetPipeline("muonroi-standard");
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Normal payment processing — retried on transient failures
    // -------------------------------------------------------------------------

    /// <summary>
    /// Processes a payment request through the resilience pipeline.
    /// <para>
    /// When <paramref name="simulateTransientFailure"/> is <see langword="true"/> the method
    /// throws <see cref="MTransientException"/> on the first attempt so you can observe the
    /// automatic retry and the <see cref="MuonroiMetrics.RetryAttemptCount"/> counter increment.
    /// </para>
    /// </summary>
    public async Task<PaymentResult> ProcessPaymentAsync(
        PaymentRequest request,
        bool simulateTransientFailure = false,
        CancellationToken cancellationToken = default)
    {
        bool alreadyFailed = false;

        _logger.Info("Processing payment — amount={Amount} currency={Currency}", request.Amount, request.Currency);

        PaymentResult result = await _pipeline.ExecuteAsync(async ct =>
        {
            if (simulateTransientFailure && !alreadyFailed)
            {
                alreadyFailed = true;
                _logger.Warn("Injecting transient failure for payment demo");

                // MTransientException is in the retry predicate — pipeline will retry automatically.
                // MuonroiMetrics.RetryAttemptCount is incremented by the OnRetry callback.
                throw new MTransientException("Payment gateway temporarily unavailable (simulated)");
            }

            // Simulate async I/O — replace with a real HttpClient call in production.
            await Task.Delay(50, ct);

            string transactionId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            _logger.Info("Payment authorised — transactionId={TransactionId}", transactionId);
            return new PaymentResult(transactionId, "Authorised", request.Amount, request.Currency);
        }, cancellationToken);

        return result;
    }

    // -------------------------------------------------------------------------
    // Circuit-breaker demo — hard failures that count toward the break threshold
    // -------------------------------------------------------------------------

    /// <summary>
    /// Simulates a hard gateway failure (non-transient <see cref="Exception"/>) that counts toward
    /// the circuit-breaker failure ratio.  After ≥5 calls with ≥50% generic failures in the
    /// 30-second sampling window the breaker opens and subsequent calls throw
    /// <see cref="BrokenCircuitException"/> immediately without reaching the gateway.
    /// </summary>
    public async Task<PaymentResult> ProcessPaymentWithHardFailureAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        _hardFailureCount++;
        _logger.Warn("Hard-failure payment attempt #{Count} (circuit-breaker demo)", _hardFailureCount);

        return await _pipeline.ExecuteAsync<PaymentResult>(async ct =>
        {
            // Generic Exception matches the circuit-breaker's ShouldHandle predicate.
            // It does NOT match the retry predicate (which only handles MTransientException /
            // HttpRequestException), so it is not retried — it feeds directly into the
            // failure-ratio counter tracked by the circuit breaker.
            await Task.CompletedTask; // satisfy the async contract before throwing
            throw new InvalidOperationException(
                $"Payment gateway hard failure #{_hardFailureCount} (circuit breaker demo)");
        }, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Custom "payment-gateway" pipeline — registered in Program.cs
    // -------------------------------------------------------------------------

    /// <summary>
    /// Processes a payment using the bespoke "payment-gateway" pipeline (5 retries, 30s timeout)
    /// registered alongside "muonroi-standard" in <c>Program.cs</c>.
    /// </summary>
    public async Task<PaymentResult> ProcessPaymentWithCustomPipelineAsync(
        PaymentRequest request,
        ResiliencePipelineProvider<string> pipelineProvider,
        CancellationToken cancellationToken = default)
    {
        ResiliencePipeline customPipeline = pipelineProvider.GetPipeline("payment-gateway");

        _logger.Info("Processing payment via 'payment-gateway' custom pipeline");

        return await customPipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(30, ct);
            string transactionId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            return new PaymentResult(transactionId, "Authorised (custom pipeline)", request.Amount, request.Currency);
        }, cancellationToken);
    }

    public void ResetHardFailureCount() => _hardFailureCount = 0;
}

/// <summary>Incoming payment request.</summary>
public sealed record PaymentRequest(decimal Amount, string Currency, string MerchantReference);

/// <summary>Result returned by <see cref="PaymentService"/>.</summary>
public sealed record PaymentResult(string TransactionId, string Status, decimal Amount, string Currency);
