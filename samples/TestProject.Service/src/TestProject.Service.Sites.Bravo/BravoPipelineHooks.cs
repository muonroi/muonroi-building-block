using Muonroi.RuleEngine.Abstractions;
using Muonroi.Tenancy.SiteProfile.Web.Pipeline;

namespace TestProject.Service.Sites.Bravo;

/// <summary>
/// Before hook for BRAVO order creation pipeline step.
/// Validates BRAVO-specific business rules: BookingNo is required in the FactBag.
/// Demonstrates ISiteStepHook pattern — no virtual method inheritance needed (per D-27).
/// </summary>
public sealed class BravoValidateOrderHook : ISiteStepHook
{
    /// <inheritdoc />
    public Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default)
    {
        // [TEST:BREAKPOINT] BravoValidateOrderHook - Execute
        var bookingNo = facts.Get<string>("order.booking_no");
        if (string.IsNullOrEmpty(bookingNo))
        {
            throw new InvalidOperationException(
                "BRAVO requires order.booking_no — cannot create order without a booking reference.");
        }

        // Signal successful validation so after-hooks and test assertions can confirm this ran
        facts.Set("bravo.validated", true);
        return Task.CompletedTask;
    }
}

/// <summary>
/// After hook for BRAVO order creation pipeline step.
/// Enriches the FactBag with BRAVO-specific post-processing data.
/// Demonstrates ISiteStepHook pattern — runs after defaultImpl, no override needed (per D-27).
/// </summary>
public sealed class BravoEnrichOrderHook : ISiteStepHook
{
    /// <inheritdoc />
    public Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default)
    {
        // [TEST:BREAKPOINT] BravoEnrichOrderHook - Execute
        facts.Set("bravo.enriched", true);
        facts.Set("bravo.enriched_at", DateTime.UtcNow.ToString("O"));
        return Task.CompletedTask;
    }
}

/// <summary>
/// A failing hook used in BestEffort resilience tests.
/// Always throws — used to verify that BestEffort mode collects errors without stopping execution.
/// </summary>
public sealed class BravoFailingHook : ISiteStepHook
{
    /// <inheritdoc />
    public Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default)
    {
        // Intentionally fail — used in resilience/BestEffort tests
        throw new InvalidOperationException("BravoFailingHook: intentional failure for BestEffort test.");
    }
}
