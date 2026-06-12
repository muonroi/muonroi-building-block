using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleEngine.CEP;
using Muonroi.RuleEngine.CEP.Abstractions;
using Muonroi.RuleEngine.CEP.Builder;
using FraudDetection.Api.Models;
using FraudDetection.Api.Services;

namespace FraudDetection.Api.Controllers;

/// <summary>
/// Represents the Fraud Transactions Controller.
/// </summary>
[ApiController]
[Route("api/transactions")]
public sealed class FraudTransactionsController(
    ICepConfigRepository repository,
    FraudMonitorService fraudMonitorService,
    IMDateTimeService dateTimeService) : ControllerBase
{
    /// <summary>
    /// Executes the Evaluate operation.
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<ActionResult<TransactionEvaluationResponse>> Evaluate(
        [FromBody] TransactionEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardId))
        {
            return BadRequest(new { message = "CardId is required." });
        }

        await EnsureDefaultConfigAsync(cancellationToken);

        TransactionEvaluationRequest normalized = request with
        {
            TimestampUtc = request.TimestampUtc == default
                ? dateTimeService.UtcNow()
                : request.TimestampUtc.ToUniversalTime()
        };

        FraudEvaluationResult result = await fraudMonitorService.EvaluateAsync(normalized, cancellationToken);
        return Ok(new TransactionEvaluationResponse
        {
            TenantId = result.TenantId,
            CardId = result.CardId,
            AlertTriggered = result.AlertTriggered,
            EventCount = result.EventCount,
            Threshold = result.Threshold,
            WindowSizeSeconds = result.WindowSizeSeconds,
            TotalAmount = result.TotalAmount
        });
    }

    private async Task EnsureDefaultConfigAsync(CancellationToken cancellationToken)
    {
        if (await repository.GetAsync(FraudMonitorService.DefaultConfigId, cancellationToken) is not null)
        {
            return;
        }

        CepConfig config = CepWindowBuilder
            .Named("High velocity cards")
            .Describe("Triggers when the same card produces at least three events in a short period.")
            .Sliding(TimeSpan.FromSeconds(60))
            .KeepEventsFor(TimeSpan.FromMinutes(5))
            .CorrelateBy("cardId")
            .WithMetadata("threshold", "3")
            .Build(FraudMonitorService.DefaultConfigId);

        await repository.SaveAsync(config, cancellationToken);
    }
}
