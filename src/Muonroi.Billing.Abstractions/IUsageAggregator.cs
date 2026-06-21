namespace Muonroi.Billing.Abstractions;

/// <summary>
/// Rolls per-tenant metered usage for a billing period into priced <see cref="UsageLineItem"/>s
/// using a <see cref="PricingPlan"/> (MON-01, D-03).
/// <para>
/// The aggregation is compute-only: it reads metered usage and produces line items. It never
/// calls a payment processor and never executes a charge. The concrete implementation lands in 17-02.
/// </para>
/// </summary>
public interface IUsageAggregator
{
    /// <summary>
    /// Aggregates the tenant's metered usage over the billing period into priced line items.
    /// </summary>
    /// <param name="tenantId">The tenant whose usage is aggregated.</param>
    /// <param name="plan">The pricing plan applied to the metered quantities.</param>
    /// <param name="periodStart">The inclusive start of the billing period.</param>
    /// <param name="periodEnd">The exclusive end of the billing period.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The priced line items for the period. No external call or charge is performed.</returns>
    Task<IReadOnlyList<UsageLineItem>> AggregateAsync(
        string tenantId,
        PricingPlan plan,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct = default);
}
