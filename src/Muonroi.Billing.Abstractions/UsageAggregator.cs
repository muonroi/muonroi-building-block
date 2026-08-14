namespace Muonroi.Billing.Abstractions;

/// <summary>
/// Default <see cref="IUsageAggregator"/> (MON-03): rolls a single tenant's metered usage —
/// read from <see cref="ITenantQuotaStore.GetUsageAsync(string, System.Threading.CancellationToken)"/> —
/// into deterministically priced <see cref="UsageLineItem"/>s via a <see cref="PricingPlan"/>.
/// <para>
/// Pricing is <c>Σ(quantity per dimension × tier unit-rate) + optional flat base</c> (D-05).
/// Per-dimension lines are emitted in ascending <see cref="QuotaType"/> enum order; the optional
/// flat-base line (identified by <see cref="UsageLineItem.FlatBaseDescription"/>) is appended LAST,
/// so the same inputs always produce identical output (deterministic, T-17-03 single-tenant read).
/// No proration, tax, multi-currency, payment call, or charge is performed.
/// </para>
/// </summary>
public sealed class UsageAggregator : IUsageAggregator
{
    private readonly ITenantQuotaStore _quotaStore;

    /// <summary>
    /// Initializes a new <see cref="UsageAggregator"/>.
    /// </summary>
    /// <param name="quotaStore">The store the per-tenant metered usage is read from.</param>
    public UsageAggregator(ITenantQuotaStore quotaStore)
        => _quotaStore = MGuard.NotNull(quotaStore);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageLineItem>> AggregateAsync(
        string tenantId,
        PricingPlan plan,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct = default)
    {
        MGuard.NotNull(plan);

        // T-17-03: read usage strictly for the supplied tenant; never enumerate other tenants.
        QuotaUsage usage = await _quotaStore.GetUsageAsync(tenantId, ct).ConfigureAwait(false);

        List<UsageLineItem> lineItems = [];

        // Deterministic ordering by QuotaType enum value so repeated calls are identical.
        foreach (KeyValuePair<QuotaType, int> entry in usage.CurrentUsage.OrderBy(e => (int)e.Key))
        {
            decimal unitRate = plan.GetUnitRate(entry.Key);
            lineItems.Add(UsageLineItem.Create(entry.Key, entry.Value, unitRate));
        }

        // Flat base appended LAST; emit none when there is nothing to charge.
        if (plan.FlatBaseAmount > 0m)
        {
            lineItems.Add(new UsageLineItem(
                Dimension: default,
                Quantity: 0,
                UnitRate: 0m,
                Amount: plan.FlatBaseAmount,
                Description: UsageLineItem.FlatBaseDescription));
        }

        return lineItems;
    }
}
